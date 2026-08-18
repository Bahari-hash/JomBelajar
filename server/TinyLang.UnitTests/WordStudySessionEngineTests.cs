using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class WordStudySessionEngineTests
{
    [Fact]
    public async Task SuccessfulReviewShouldAdvanceSchedule()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "review@example.test", PasswordHash = "hash" };
        var word = CreateWord("review", 1);
        db.AddRange(user, word);
        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = word.Id,
            FirstStudiedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z"),
            LastStudiedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z"),
            ReviewStage = 0,
            NextReviewAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z")
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartReviewSessionAsync(user.Id, TestContext.Current.CancellationToken);
        var memorized = await service.SubmitReviewMemorizationAsync(
            user.Id,
            session.Id,
            session.CurrentItem!.ItemId,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);
        var completed = await service.SubmitReviewSpellingAsync(
            user.Id,
            session.Id,
            memorized.Session.CurrentItem!.ItemId,
            new SubmitWordSpellingRequest
            {
                Answer = "review",
                ItemConcurrencyStamp = memorized.Session.CurrentItem.ItemConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        completed.Session.Status.Should().Be(WordStudySessionStatus.Completed);
        var progress = await db.UserWordProgress.SingleAsync(
            value => value.UserId == user.Id && value.WordId == word.Id,
            TestContext.Current.CancellationToken);
        progress.ReviewStage.Should().Be(1);
        progress.SuccessfulReviewCount.Should().Be(1);
        progress.NextReviewAt.Should().Be(DateTimeOffset.Parse("2026-08-20T03:00:00Z"));
    }

    [Fact]
    public async Task SpellingShouldRequeueIncorrectAndCompleteCorrectLearning()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "spelling@example.test", PasswordHash = "hash" };
        var word = CreateWord("école", 1);
        db.AddRange(user, word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(user.Id, TestContext.Current.CancellationToken);
        var memorized = await service.SubmitLearningMemorizationAsync(
            user.Id,
            session.Id,
            session.CurrentItem!.ItemId,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);

        var incorrect = await service.SubmitLearningSpellingAsync(
            user.Id,
            session.Id,
            memorized.Session.CurrentItem!.ItemId,
            new SubmitWordSpellingRequest
            {
                Answer = "wrong",
                ItemConcurrencyStamp = memorized.Session.CurrentItem.ItemConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        incorrect.SpellingResult.Should().Be(WordSpellingResult.Incorrect);
        incorrect.Session.Status.Should().Be(WordStudySessionStatus.Active);

        var correct = await service.SubmitLearningSpellingAsync(
            user.Id,
            session.Id,
            incorrect.Session.CurrentItem!.ItemId,
            new SubmitWordSpellingRequest
            {
                Answer = " ÉCOLE ",
                ItemConcurrencyStamp = incorrect.Session.CurrentItem.ItemConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        correct.SpellingResult.Should().Be(WordSpellingResult.Correct);
        correct.Session.Status.Should().Be(WordStudySessionStatus.Completed);
        var progress = await db.UserWordProgress.SingleAsync(
            value => value.UserId == user.Id && value.WordId == word.Id,
            TestContext.Current.CancellationToken);
        progress.ReviewStage.Should().Be(0);
        progress.NextReviewAt.Should().Be(DateTimeOffset.Parse("2026-08-19T03:00:00Z"));
    }

    [Fact]
    public async Task ForgottenMemorizationShouldMoveItemToQueueTail()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "engine@example.test", PasswordHash = "hash" };
        var first = CreateWord("first", 1);
        var second = CreateWord("second", 2);
        db.AddRange(user, first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(user.Id, TestContext.Current.CancellationToken);
        var stored = await db.WordStudySessions.Include(value => value.Items)
            .SingleAsync(value => value.Id == session.Id, TestContext.Current.CancellationToken);
        var firstItem = stored.Items.Single(value => value.WordId == first.Id);

        var result = await new WordStudySessionEngine(
                db,
                new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")))
            .SubmitMemorizationAsync(
                user.Id,
                session.Id,
                firstItem.Id,
                WordStudySessionType.Learning,
                new SubmitWordMemorizationRequest(
                    WordMemorizationResult.Forgotten,
                    firstItem.ConcurrencyStamp),
                TestContext.Current.CancellationToken);

        result.Session.CurrentItem!.WordId.Should().Be(second.Id);
        firstItem.HadMemorizationFailure.Should().BeTrue();
        firstItem.MemorizationAttemptCount.Should().Be(1);
    }

    [Fact]
    public async Task RememberingAllItemsShouldSwitchToSpelling()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "phase@example.test", PasswordHash = "hash" };
        var word = CreateWord("phase", 1);
        db.AddRange(user, word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(user.Id, TestContext.Current.CancellationToken);
        var item = await db.WordStudySessionItems.SingleAsync(
            value => value.SessionId == session.Id,
            TestContext.Current.CancellationToken);

        var result = await new WordStudySessionEngine(
                db,
                new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")))
            .SubmitMemorizationAsync(
                user.Id,
                session.Id,
                item.Id,
                WordStudySessionType.Learning,
                new SubmitWordMemorizationRequest(
                    WordMemorizationResult.Remembered,
                    item.ConcurrencyStamp),
                TestContext.Current.CancellationToken);

        result.Session.Phase.Should().Be(WordStudyPhase.Spelling);
        result.Session.CurrentItem!.Spelling.Should().NotBeNull();
        result.Session.CurrentItem.Memorization.Should().BeNull();
    }

    [Fact]
    public async Task StaleQueueSubmissionShouldBeRejected()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "queue@example.test", PasswordHash = "hash" };
        db.AddRange(user, CreateWord("one", 1), CreateWord("two", 2));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(user.Id, TestContext.Current.CancellationToken);
        var items = await db.WordStudySessionItems
            .Where(value => value.SessionId == session.Id)
            .OrderBy(value => value.Position)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        var act = () => new WordStudySessionEngine(
                db,
                new TestTimeProvider(DateTimeOffset.UtcNow))
            .SubmitMemorizationAsync(
                user.Id,
                session.Id,
                items[1].Id,
                WordStudySessionType.Learning,
                new SubmitWordMemorizationRequest(
                    WordMemorizationResult.Remembered,
                    items[1].ConcurrencyStamp),
                TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
    }

    private static WordStudyService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")),
            Mock.Of<ILogger<WordStudyService>>());

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Word CreateWord(string headword, long order)
        => new()
        {
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
            StudyOrder = order,
            Senses =
            [new WordSense
            {
                PartOfSpeech = PartOfSpeech.Noun,
                Definition = headword,
                SortOrder = 0
            }]
        };
}
