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

public sealed class FsrsSessionTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.Parse("2026-10-03T00:00:00Z");
    private static CancellationToken Ct => TestContext.Current.CancellationToken;
    private static ApplicationDbContext Db() => new(new DbContextOptionsBuilder<ApplicationDbContext>()
        .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
    private static WordStudyService Service(ApplicationDbContext db, TimeProvider clock) =>
        new(db, Mock.Of<IDatabaseExceptionClassifier>(), clock);
    private static async Task<User> Seed(ApplicationDbContext db)
    {
        var user = new User { Email = "fsrs@test.example", PasswordHash = "hash" };
        var word = new Word { Headword = "buku", NormalizedHeadword = "BUKU", StudyOrder = 1,
            Senses = [new WordSense { PartOfSpeech = PartOfSpeech.Noun, Definition = "书" }] };
        db.AddRange(user, word); await db.SaveChangesAsync(Ct); return user;
    }
    private static Task<WordStudyCommandResponse> Rate(WordStudyService service, Guid user,
        WordStudySessionStateResponse session, WordMemorizationResult rating) => service.SubmitLearningMemorizationAsync(
        user, session.Id, session.CurrentItem!.ItemId,
        new(rating, session.CurrentItem.ItemConcurrencyStamp), Ct);

    [Fact]
    public async Task ExitSpellingPreservesCorrectCountAndSchedule()
    {
        await using var db = Db(); var user = await Seed(db);
        db.Words.Add(new Word { Headword = "air", NormalizedHeadword = "AIR", StudyOrder = 2,
            Senses = [new WordSense { PartOfSpeech = PartOfSpeech.Noun, Definition = "水" }] });
        await db.SaveChangesAsync(Ct);
        var service = Service(db, new TestTimeProvider(Now));
        var session = await service.StartLearningSessionAsync(user.Id, Ct);
        var first = await Rate(service, user.Id, session, WordMemorizationResult.Easy);
        await Rate(service, user.Id, first.Session, WordMemorizationResult.Easy);
        var spelling = await service.FinishSummaryAsync(user.Id, session.Id, WordStudySessionType.Learning, false, Ct);
        await service.SubmitLearningSpellingAsync(user.Id, session.Id, spelling.CurrentItem!.ItemId,
            new() { Answer = "buku", ItemConcurrencyStamp = spelling.CurrentItem.ItemConcurrencyStamp }, Ct);
        var due = await db.UserWordProgress.OrderBy(x => x.WordId).Select(x => x.NextReviewAt).ToArrayAsync(Ct);
        var done = await service.FinishSummaryAsync(user.Id, session.Id, WordStudySessionType.Learning, true, Ct);
        done.Status.Should().Be(WordStudySessionStatus.Completed); done.CompletedCount.Should().Be(2);
        done.SpellingPassedCount.Should().Be(1);
        (await db.UserWordProgress.OrderBy(x => x.WordId).Select(x => x.NextReviewAt).ToArrayAsync(Ct)).Should().Equal(due);
        (await db.WordStudyActivities.CountAsync(Ct)).Should().Be(2);
    }

    [Theory]
    [InlineData(WordStudySessionType.Learning)]
    [InlineData(WordStudySessionType.Review)]
    public async Task SummaryPersistsAndSkipCompletesWithoutChangingScheduleOrClaimingSpelling(WordStudySessionType type)
    {
        await using var db = Db(); var user = await Seed(db); var word = await db.Words.SingleAsync(Ct);
        if (type == WordStudySessionType.Review)
        {
            db.UserWordProgress.Add(new UserWordProgress { UserId = user.Id, WordId = word.Id,
                FirstStudiedAt = Now.AddDays(-1), LastStudiedAt = Now.AddDays(-1), NextReviewAt = Now });
            await db.SaveChangesAsync(Ct);
        }
        var service = Service(db, new TestTimeProvider(Now));
        var session = type == WordStudySessionType.Learning ? await service.StartLearningSessionAsync(user.Id, Ct) : await service.StartReviewSessionAsync(user.Id, Ct);
        var engine = new WordStudySessionEngine(db, new TestTimeProvider(Now));
        var rated = await engine.SubmitMemorizationAsync(user.Id, session.Id, session.CurrentItem!.ItemId, type,
            new(WordMemorizationResult.Easy, session.CurrentItem.ItemConcurrencyStamp), Ct);
        rated.Session.Phase.Should().Be(WordStudyPhase.Summary); rated.Session.CurrentItem.Should().BeNull();
        rated.Session.Summary!.EasyCount.Should().Be(1); rated.Session.Summary.WordCount.Should().Be(1);
        var due = (await db.UserWordProgress.SingleAsync(Ct)).NextReviewAt;
        db.ChangeTracker.Clear();
        var reload = type == WordStudySessionType.Learning ? await service.GetLearningSessionAsync(user.Id, session.Id, Ct) : await service.GetReviewSessionAsync(user.Id, session.Id, Ct);
        reload.Phase.Should().Be(WordStudyPhase.Summary);
        var result = await service.FinishSummaryAsync(user.Id, session.Id, type, true, Ct);
        result.Status.Should().Be(WordStudySessionStatus.Completed); result.SpellingPassedCount.Should().Be(0);
        (await db.UserWordProgress.SingleAsync(Ct)).NextReviewAt.Should().Be(due);
        (await db.WordStudyActivities.CountAsync(Ct)).Should().Be(1);
        var repeated = () => service.FinishSummaryAsync(user.Id, session.Id, type, true, Ct);
        await repeated.Should().ThrowAsync<ConflictException>();
        (await db.WordStudyActivities.CountAsync(Ct)).Should().Be(1);
    }

    [Theory]
    [InlineData(WordMemorizationResult.Again)]
    [InlineData(WordMemorizationResult.Hard)]
    [InlineData(WordMemorizationResult.Good)]
    public async Task SubDayRatingsGoToQueueTailWithoutWaiting(WordMemorizationResult rating)
    {
        await using var db = Db(); var user = await Seed(db);
        db.Words.Add(new Word { Headword = "air", NormalizedHeadword = "AIR", StudyOrder = 2,
            Senses = [new WordSense { PartOfSpeech = PartOfSpeech.Noun, Definition = "水" }] });
        await db.SaveChangesAsync(Ct);
        var service = Service(db, new TestTimeProvider(Now));
        var session = await service.StartLearningSessionAsync(user.Id, Ct);
        var first = await Rate(service, user.Id, session, rating);
        first.Session.CurrentItem!.Memorization!.Headword.Should().Be("air");
        first.Session.NextAvailableAt.Should().BeNull();
        var second = await Rate(service, user.Id, first.Session, WordMemorizationResult.Good);
        second.Session.CurrentItem!.Memorization!.Headword.Should().Be("buku");
        db.ChangeTracker.Clear();
        var reload = await service.GetLearningSessionAsync(user.Id, session.Id, Ct);
        reload.CurrentItem!.Memorization!.Headword.Should().Be("buku");
        // Existing sessions with old timers must resume immediately as well.
        var oldItem = await db.WordStudySessionItems.SingleAsync(x => x.Id == reload.CurrentItem.ItemId, Ct);
        oldItem.MemorizationAvailableAt = Now.AddMinutes(10); await db.SaveChangesAsync(Ct);
        var restored = await service.GetLearningSessionAsync(user.Id, session.Id, Ct);
        restored.CurrentItem!.ItemId.Should().Be(oldItem.Id);
        var finished = await Rate(service, user.Id, restored, WordMemorizationResult.Easy);
        finished.Session.CurrentItem!.Memorization!.Headword.Should().Be("air");
    }

    [Fact]
    public async Task GoodRepeatsImmediatelyThenSpellingDoesNotReschedule()
    {
        await using var db = Db(); var user = await Seed(db); var clock = new TestTimeProvider(Now);
        var service = Service(db, clock); var session = await service.StartLearningSessionAsync(user.Id, Ct);
        var previews = session.CurrentItem!.Memorization!.RatingPreviews!;
        previews.Select(x => x.IntervalSeconds).Should().Equal(60, 330, 600, 16 * 86400);
        var first = await Rate(service, user.Id, session, WordMemorizationResult.Good);
        first.Session.CurrentItem.Should().NotBeNull(); first.Session.Status.Should().Be(WordStudySessionStatus.Active);
        first.Session.NextAvailableAt.Should().BeNull();
        (await db.UserWordProgress.SingleAsync(Ct)).NextReviewAt.Should().Be(previews[2].DueAt);
        db.ChangeTracker.Clear();
        var waiting = await service.GetLearningSessionAsync(user.Id, session.Id, Ct);
        waiting.CurrentItem.Should().NotBeNull();
        (await service.GetReviewOverviewAsync(user.Id, Ct)).DueCount.Should().Be(0);
        (await service.GetReviewOverviewAsync(user.Id, Ct)).DueCount.Should().Be(0); // reserved by learning session
        var resumed = await service.GetLearningSessionAsync(user.Id, session.Id, Ct);
        var rated = await Rate(service, user.Id, resumed, WordMemorizationResult.Good);
        rated.Session.Phase.Should().Be(WordStudyPhase.Summary);
        rated = rated with { Session = await service.FinishSummaryAsync(user.Id, session.Id, WordStudySessionType.Learning, false, Ct) };
        var due = (await db.UserWordProgress.SingleAsync(Ct)).NextReviewAt;
        var incorrect = await service.SubmitLearningSpellingAsync(user.Id, session.Id, rated.Session.CurrentItem!.ItemId,
            new() { Answer = "", Skip = true, ItemConcurrencyStamp = rated.Session.CurrentItem.ItemConcurrencyStamp }, Ct);
        var complete = await service.SubmitLearningSpellingAsync(user.Id, session.Id, incorrect.Session.CurrentItem!.ItemId,
            new() { Answer = "buku", ItemConcurrencyStamp = incorrect.Session.CurrentItem.ItemConcurrencyStamp }, Ct);
        complete.Session.Status.Should().Be(WordStudySessionStatus.Completed);
        (await db.UserWordProgress.SingleAsync(Ct)).NextReviewAt.Should().Be(due);
        (await db.WordReviewLogs.CountAsync(Ct)).Should().Be(2);
        (await db.WordStudyActivities.CountAsync(Ct)).Should().Be(1);
        (await service.GetLearningOverviewAsync(user.Id, Ct)).TodayLearnedCount.Should().Be(1);
    }

    [Fact]
    public async Task RepeatedRatingRejectedButNextQueuedAttemptAccepted()
    {
        await using var db = Db(); var user = await Seed(db); var clock = new TestTimeProvider(Now);
        var service = Service(db, clock); var session = await service.StartLearningSessionAsync(user.Id, Ct);
        await Rate(service, user.Id, session, WordMemorizationResult.Again);
        var repeat = () => Rate(service, user.Id, session, WordMemorizationResult.Again);
        await repeat.Should().ThrowAsync<ConflictException>();
        var item = await db.WordStudySessionItems.SingleAsync(Ct);
        var early = () => service.SubmitLearningMemorizationAsync(user.Id, session.Id, item.Id,
            new(WordMemorizationResult.Good, item.ConcurrencyStamp), Ct);
        await early();
        (await db.WordReviewLogs.CountAsync(Ct)).Should().Be(2);
        clock.Advance(TimeSpan.FromMinutes(1));
        var due = await service.GetLearningSessionAsync(user.Id, session.Id, Ct);
        due.CurrentItem.Should().NotBeNull();
        var wrongUser = () => service.GetLearningSessionAsync(Guid.NewGuid(), session.Id, Ct);
        await wrongUser.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task LegacyProgressIsUnchangedUntilRatedAndUsesExplicitImportFlag()
    {
        await using var db = Db(); var user = await Seed(db); var word = await db.Words.SingleAsync(Ct);
        var due = Now.AddDays(-2);
        db.UserWordProgress.Add(new() { UserId = user.Id, WordId = word.Id, ReviewStage = 4,
            NextReviewAt = due, FirstStudiedAt = Now.AddDays(-30), LastStudiedAt = Now.AddDays(-17) });
        await db.SaveChangesAsync(Ct);
        var service = Service(db, new TestTimeProvider(Now));
        var session = await service.StartReviewSessionAsync(user.Id, Ct);
        var progress = await db.UserWordProgress.SingleAsync(Ct);
        progress.NextReviewAt.Should().Be(due); progress.FsrsState.Should().BeNull();
        await service.SubmitReviewMemorizationAsync(user.Id, session.Id, session.CurrentItem!.ItemId,
            new(WordMemorizationResult.Easy, session.CurrentItem.ItemConcurrencyStamp), Ct);
        progress.ImportedFromLegacy.Should().BeTrue(); progress.FsrsState.Should().Be("Review");
        var log = await db.WordReviewLogs.SingleAsync(Ct);
        log.ImportedFromLegacy.Should().BeTrue(); log.SchedulerVersion.Should().Be(FsrsWordScheduler.Version);
    }

    [Fact]
    public void HigherRetentionProducesShorterIntervalsWithoutMutatingPreviewCard()
    {
        var progress = new UserWordProgress { FsrsState = "Review", Stability = 20, Difficulty = 5,
            LastRatedAt = Now.AddDays(-20), NextReviewAt = Now };
        var normal = FsrsWordScheduler.Rate(progress, WordMemorizationResult.Good, Now, .9);
        var high = FsrsWordScheduler.Rate(progress, WordMemorizationResult.Good, Now, .95);
        high.Due.Should().BeBefore(normal.Due); progress.Stability.Should().Be(20);
        progress.NextReviewAt.Should().Be(Now);
    }
}
