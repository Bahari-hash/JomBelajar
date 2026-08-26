using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
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
        (await db.WordStudyActivities.CountAsync(value =>
            value.UserId == user.Id && value.ActivityType == WordStudyActivityType.Review,
            TestContext.Current.CancellationToken)).Should().Be(1);
        (await db.WordStudyCheckIns.CountAsync(value => value.UserId == user.Id,
            TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task ReviewFailureShouldResetScheduleAfterEventualSuccess()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "failed-review@example.test", PasswordHash = "hash" };
        var word = CreateWord("retry", 1);
        db.AddRange(user, word);
        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = word.Id,
            FirstStudiedAt = DateTimeOffset.Parse("2026-08-01T03:00:00Z"),
            LastStudiedAt = DateTimeOffset.Parse("2026-08-01T03:00:00Z"),
            ReviewStage = 4,
            NextReviewAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z")
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartReviewSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        var forgotten = await service.SubmitReviewMemorizationAsync(
            user.Id,
            session.Id,
            session.CurrentItem!.ItemId,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Forgotten,
                session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);
        var remembered = await service.SubmitReviewMemorizationAsync(
            user.Id,
            session.Id,
            forgotten.Session.CurrentItem!.ItemId,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                forgotten.Session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);
        await service.SubmitReviewSpellingAsync(
            user.Id,
            session.Id,
            remembered.Session.CurrentItem!.ItemId,
            new SubmitWordSpellingRequest
            {
                Answer = "retry",
                ItemConcurrencyStamp = remembered.Session.CurrentItem.ItemConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        var progress = await db.UserWordProgress.SingleAsync(
            value => value.UserId == user.Id && value.WordId == word.Id,
            TestContext.Current.CancellationToken);
        progress.ReviewStage.Should().Be(0);
        progress.FailedReviewCount.Should().Be(1);
        progress.SuccessfulReviewCount.Should().Be(0);
        progress.NextReviewAt.Should().Be(DateTimeOffset.Parse("2026-08-19T03:00:00Z"));
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
        (await db.WordStudyActivities.CountAsync(value =>
            value.UserId == user.Id && value.ActivityType == WordStudyActivityType.Learning,
            TestContext.Current.CancellationToken)).Should().Be(1);
        (await db.WordStudyCheckIns.CountAsync(value => value.UserId == user.Id,
            TestContext.Current.CancellationToken)).Should().Be(1);
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
    public async Task RememberedItemShouldAdvanceToNextMemorizationItem()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Email = "advance@example.test",
            PasswordHash = "hash",
            DailyWordStudyCount = 2
        };
        var first = CreateWord("first", 1);
        var second = CreateWord("second", 2);
        db.AddRange(user, first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);

        var result = await service.SubmitLearningMemorizationAsync(
            user.Id,
            session.Id,
            session.CurrentItem!.ItemId,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);

        result.Session.Phase.Should().Be(WordStudyPhase.Memorization);
        result.Session.CurrentItem!.WordId.Should().Be(second.Id);
        result.Session.MemorizationPassedCount.Should().Be(1);
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

    [Fact]
    public async Task CommandShouldSkipAnInvisibleWordWithoutCreatingProgress()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "hidden@example.test", PasswordHash = "hash" };
        var hiddenWord = new Word
        {
            Headword = "hidden",
            NormalizedHeadword = "HIDDEN",
            StudyOrder = 1
        };
        var session = new WordStudySession
        {
            UserId = user.Id,
            User = user,
            RequestedCount = 1,
            ActualCount = 1,
            SessionType = WordStudySessionType.Learning,
            Phase = WordStudyPhase.Memorization,
            StartedAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z"),
            Items =
            [new WordStudySessionItem
            {
                WordId = hiddenWord.Id,
                Position = 0,
                MemorizationQueueOrder = 0,
                SpellingQueueOrder = 0
            }]
        };
        db.AddRange(user, hiddenWord, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        (await db.WordStudySessions.AnyAsync(
            value => value.Id == session.Id && value.UserId == user.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        var service = CreateService(db);
        var item = session.Items.Single();

        var result = await service.SubmitLearningMemorizationAsync(
            user.Id,
            session.Id,
            item.Id,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                item.ConcurrencyStamp),
            TestContext.Current.CancellationToken);

        result.Session.Status.Should().Be(WordStudySessionStatus.Completed);
        result.Session.SkippedCount.Should().Be(1);
        (await db.UserWordProgress.AnyAsync(
            value => value.UserId == user.Id && value.WordId == hiddenWord.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await db.WordStudySessionItems.SingleAsync(
            value => value.Id == item.Id,
            TestContext.Current.CancellationToken)).Status
            .Should().Be(WordStudySessionItemStatus.Skipped);
    }

    [Fact]
    public async Task MemorizationShouldPersistSkippedItemAndAdvanceToVisibleWord()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "hidden-next@example.test", PasswordHash = "hash" };
        var hidden = new Word
        {
            Headword = "hidden",
            NormalizedHeadword = "HIDDEN",
            StudyOrder = 1
        };
        var visible = CreateWord("visible", 2);
        var session = CreateSession(user, WordStudyPhase.Memorization, hidden, visible);
        db.AddRange(user, hidden, visible, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hiddenItem = session.Items.Single(value => value.WordId == hidden.Id);

        var result = await CreateService(db).SubmitLearningMemorizationAsync(
            user.Id,
            session.Id,
            hiddenItem.Id,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                hiddenItem.ConcurrencyStamp),
            TestContext.Current.CancellationToken);

        result.Session.CurrentItem!.WordId.Should().Be(visible.Id);
        (await db.WordStudySessionItems.SingleAsync(
            value => value.Id == hiddenItem.Id,
            TestContext.Current.CancellationToken)).Status
            .Should().Be(WordStudySessionItemStatus.Skipped);
    }

    [Fact]
    public async Task SpellingShouldPersistSkippedItemAndAdvanceToVisibleWord()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "hidden-spelling@example.test", PasswordHash = "hash" };
        var hidden = new Word
        {
            Headword = "hidden",
            NormalizedHeadword = "HIDDEN",
            StudyOrder = 1
        };
        var visible = CreateWord("visible", 2);
        var session = CreateSession(user, WordStudyPhase.Spelling, hidden, visible);
        foreach (var item in session.Items)
            item.MemorizationPassedAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z");
        db.AddRange(user, hidden, visible, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hiddenItem = session.Items.Single(value => value.WordId == hidden.Id);

        var result = await CreateService(db).SubmitLearningSpellingAsync(
            user.Id,
            session.Id,
            hiddenItem.Id,
            new SubmitWordSpellingRequest
            {
                Answer = "hidden",
                ItemConcurrencyStamp = hiddenItem.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        result.SpellingResult.Should().BeNull();
        result.Session.CurrentItem!.WordId.Should().Be(visible.Id);
        (await db.WordStudySessionItems.SingleAsync(
            value => value.Id == hiddenItem.Id,
            TestContext.Current.CancellationToken)).Status
            .Should().Be(WordStudySessionItemStatus.Skipped);
    }

    [Fact]
    public async Task ExcludeShouldPersistSkippedItemAndAdvanceToVisibleWord()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "hidden-exclude@example.test", PasswordHash = "hash" };
        var hidden = new Word
        {
            Headword = "hidden",
            NormalizedHeadword = "HIDDEN",
            StudyOrder = 1
        };
        var visible = CreateWord("visible", 2);
        var session = CreateSession(
            user,
            WordStudyPhase.Memorization,
            hidden,
            visible,
            WordStudySessionType.Review);
        db.AddRange(user, hidden, visible, session);
        db.UserWordProgress.AddRange(
            CreateProgress(user.Id, hidden.Id),
            CreateProgress(user.Id, visible.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var hiddenItem = session.Items.Single(value => value.WordId == hidden.Id);

        var result = await CreateService(db).ExcludeFromReviewAsync(
            user.Id,
            session.Id,
            hiddenItem.Id,
            new ExcludeWordFromReviewRequest(hiddenItem.ConcurrencyStamp),
            TestContext.Current.CancellationToken);

        result.Session.CurrentItem!.WordId.Should().Be(visible.Id);
        (await db.UserWordProgress.SingleAsync(
            value => value.UserId == user.Id && value.WordId == hidden.Id,
            TestContext.Current.CancellationToken)).IsReviewExcluded.Should().BeFalse();
        (await db.WordStudySessionItems.SingleAsync(
            value => value.Id == hiddenItem.Id,
            TestContext.Current.CancellationToken)).Status
            .Should().Be(WordStudySessionItemStatus.Skipped);
    }

    [Fact]
    public async Task CompletedResultsShouldRejectTheWrongSessionType()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "results@example.test", PasswordHash = "hash" };
        var word = CreateWord("results", 1);
        var session = new WordStudySession
        {
            User = user,
            UserId = user.Id,
            RequestedCount = 1,
            ActualCount = 1,
            SessionType = WordStudySessionType.Review,
            Phase = WordStudyPhase.Spelling,
            Status = WordStudySessionStatus.Completed,
            StartedAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z"),
            CompletedAt = DateTimeOffset.Parse("2026-08-18T03:01:00Z"),
            Items =
            [new WordStudySessionItem
            {
                Word = word,
                WordId = word.Id,
                Position = 0,
                Status = WordStudySessionItemStatus.Completed,
                CompletedAt = DateTimeOffset.Parse("2026-08-18T03:01:00Z")
            }]
        };
        db.AddRange(user, word, session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var act = () => service.GetCompletedSessionItemsAsync(
            user.Id,
            session.Id,
            WordStudySessionType.Learning,
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExcludingCurrentSpellingItemShouldCompleteWithoutReviewCount()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "exclude@example.test", PasswordHash = "hash" };
        var word = CreateWord("exclude", 1);
        db.AddRange(user, word);
        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = word.Id,
            FirstStudiedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z"),
            LastStudiedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z"),
            NextReviewAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z")
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.StartReviewSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        var spelling = await service.SubmitReviewMemorizationAsync(
            user.Id,
            session.Id,
            session.CurrentItem!.ItemId,
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);

        var result = await service.ExcludeFromReviewAsync(
            user.Id,
            session.Id,
            spelling.Session.CurrentItem!.ItemId,
            new ExcludeWordFromReviewRequest(
                spelling.Session.CurrentItem.ItemConcurrencyStamp),
            TestContext.Current.CancellationToken);

        result.Session.Status.Should().Be(WordStudySessionStatus.Completed);
        result.Session.ExcludedCount.Should().Be(1);
        var progress = await db.UserWordProgress.SingleAsync(
            value => value.UserId == user.Id && value.WordId == word.Id,
            TestContext.Current.CancellationToken);
        progress.IsReviewExcluded.Should().BeTrue();
        progress.ReviewCount.Should().Be(0);
        progress.NextReviewAt.Should().BeNull();
    }

    [Fact]
    public async Task ExcludingCurrentItemInFreshRequestShouldProjectNextWord()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "exclude-next@example.test", PasswordHash = "hash" };
        var first = CreateWord("first", 1);
        var second = CreateWord("second", 2);
        var session = CreateSession(
            user,
            WordStudyPhase.Memorization,
            first,
            second,
            WordStudySessionType.Review);
        db.AddRange(user, first, second, session);
        db.UserWordProgress.AddRange(
            CreateProgress(user.Id, first.Id),
            CreateProgress(user.Id, second.Id));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var firstItem = session.Items.Single(value => value.WordId == first.Id);
        var firstItemId = firstItem.Id;
        var firstItemConcurrencyStamp = firstItem.ConcurrencyStamp;
        db.ChangeTracker.Clear();

        var result = await CreateService(db).ExcludeFromReviewAsync(
            user.Id,
            session.Id,
            firstItemId,
            new ExcludeWordFromReviewRequest(firstItemConcurrencyStamp),
            TestContext.Current.CancellationToken);

        result.Session.CurrentItem!.WordId.Should().Be(second.Id);
        result.Session.ExcludedCount.Should().Be(1);
    }

    private static WordStudyService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")));

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static WordStudySession CreateSession(
        User user,
        WordStudyPhase phase,
        Word first,
        Word second,
        WordStudySessionType sessionType = WordStudySessionType.Learning)
        => new()
        {
            User = user,
            UserId = user.Id,
            RequestedCount = 2,
            ActualCount = 2,
            SessionType = sessionType,
            Phase = phase,
            StartedAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z"),
            Items =
            [
                new WordStudySessionItem
                {
                    Word = first,
                    WordId = first.Id,
                    Position = 0,
                    MemorizationQueueOrder = 0,
                    SpellingQueueOrder = 0
                },
                new WordStudySessionItem
                {
                    Word = second,
                    WordId = second.Id,
                    Position = 1,
                    MemorizationQueueOrder = 1,
                    SpellingQueueOrder = 1
                }
            ]
        };

    private static UserWordProgress CreateProgress(Guid userId, Guid wordId)
        => new()
        {
            UserId = userId,
            WordId = wordId,
            FirstStudiedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z"),
            LastStudiedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z"),
            NextReviewAt = DateTimeOffset.Parse("2026-08-18T03:00:00Z")
        };

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
