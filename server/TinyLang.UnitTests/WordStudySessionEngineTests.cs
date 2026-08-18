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
