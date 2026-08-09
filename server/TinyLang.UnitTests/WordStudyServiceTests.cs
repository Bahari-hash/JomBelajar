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

/// <summary>
/// 验证单词背诵会话的抽词、状态转换、进度累计和用户隔离。
/// </summary>
public sealed class WordStudyServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 29, 6, 30, 0, TimeSpan.Zero);

    [Fact]
    public async Task StartTodayShouldUseSavedDailyCount()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Username = "daily",
            Email = "daily@example.test",
            PasswordHash = "hash",
            DailyWordStudyCount = 2
        };
        db.Users.Add(user);
        db.Words.AddRange(
            CreateVisibleWord("one", "en", Now),
            CreateVisibleWord("two", "en", Now.AddMinutes(-1)),
            CreateVisibleWord("three", "en", Now.AddMinutes(-2)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userId = user.Id;

        var response = await CreateService(db).StartTodayAsync(
            userId,
            TestContext.Current.CancellationToken);

        response.RequestedCount.Should().Be(2);
        response.ActualCount.Should().Be(2);
    }

    [Fact]
    public async Task StartTodayShouldAbandonPreviousDayActiveSession()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Username = "cross-day",
            Email = "cross-day@example.test",
            PasswordHash = "hash",
            DailyWordStudyCount = 1
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userId = user.Id;
        db.Words.AddRange(
            CreateVisibleWord("old", "en", Now),
            CreateVisibleWord("new", "en", Now.AddMinutes(-1)));
        var oldSession = new WordStudySession
        {
            UserId = userId,
            RequestedCount = 1,
            ActualCount = 1,
            SelectionMode = WordStudySelectionMode.Sequential,
            StartedAt = Now.AddDays(-1),
            StudyDateUtc = new DateTimeOffset(
                Now.AddDays(-1).UtcDateTime.Date,
                TimeSpan.Zero),
            Items = []
        };
        db.WordStudySessions.Add(oldSession);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await CreateService(db).StartTodayAsync(
            userId,
            TestContext.Current.CancellationToken);

        response.Id.Should().NotBe(oldSession.Id);
        var storedOld = await db.WordStudySessions.SingleAsync(
            value => value.Id == oldSession.Id,
            TestContext.Current.CancellationToken);
        storedOld.Status.Should().Be(WordStudySessionStatus.Abandoned);
        response.StartedAt.Date.Should().Be(Now.Date);
    }

    [Fact]
    public async Task StartTodayShouldResumeSameDayActiveSession()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Username = "same-day",
            Email = "same-day@example.test",
            PasswordHash = "hash",
            DailyWordStudyCount = 1
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var userId = user.Id;
        var word = CreateVisibleWord("same", "en", Now);
        db.Words.Add(word);
        var session = new WordStudySession
        {
            UserId = userId,
            RequestedCount = 1,
            ActualCount = 1,
            SelectionMode = WordStudySelectionMode.Sequential,
            StartedAt = Now,
            StudyDateUtc = new DateTimeOffset(Now.UtcDateTime.Date, TimeSpan.Zero),
            Items = [new WordStudySessionItem { WordId = word.Id, Position = 0 }]
        };
        db.WordStudySessions.Add(session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await CreateService(db).StartTodayAsync(
            userId,
            TestContext.Current.CancellationToken);

        response.Id.Should().Be(session.Id);
    }

    /// <summary>
    /// 验证 Sequential 排除当前用户已有进度、应用语言筛选并如实返回候选不足。
    /// </summary>
    [Fact]
    public async Task SequentialCreationShouldExcludeStudiedAndReturnActualCount()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var newest = CreateVisibleWord("newest", "en", Now.AddMinutes(-1));
        var studied = CreateVisibleWord("studied", "en", Now.AddMinutes(-2));
        var otherUserStudied = CreateVisibleWord(
            "other-user",
            "en",
            Now.AddMinutes(-3));
        var french = CreateVisibleWord("bonjour", "fr", Now.AddMinutes(-4));
        db.Words.AddRange(newest, studied, otherUserStudied, french);
        db.UserWordProgress.AddRange(
            CreateProgress(userId, studied.Id, Now.AddDays(-1)),
            CreateProgress(otherUserId, otherUserStudied.Id, Now.AddDays(-1)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest
            {
                WordCount = 5,
                LanguageTag = "EN",
                SelectionMode = WordStudySelectionMode.Sequential
            },
            TestContext.Current.CancellationToken);

        response.RequestedCount.Should().Be(5);
        response.ActualCount.Should().Be(2);
        response.LanguageTag.Should().Be("en");
        var items = await db.WordStudySessionItems.AsNoTracking()
            .Where(value => value.SessionId == response.Id)
            .OrderBy(value => value.Position)
            .ToListAsync(TestContext.Current.CancellationToken);
        items.Select(value => value.WordId).Should()
            .Equal(newest.Id, otherUserStudied.Id);
        items.Select(value => value.Position).Should().Equal(0, 1);
        var active = await service.GetActiveSessionAsync(
            userId,
            TestContext.Current.CancellationToken);
        active.Should().NotBeNull();
        active?.Id.Should().Be(response.Id);
        (await service.GetActiveSessionAsync(
            otherUserId,
            TestContext.Current.CancellationToken)).Should().BeNull();
    }

    /// <summary>
    /// 验证包含历史词时先选新词，再按最久未学习顺序补足。
    /// </summary>
    [Fact]
    public async Task SequentialIncludingStudiedShouldPrioritizeNewThenOldest()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var newWord = CreateVisibleWord("new", "en", Now);
        var oldest = CreateVisibleWord("oldest", "en", Now.AddMinutes(-1));
        var recent = CreateVisibleWord("recent", "en", Now.AddMinutes(-2));
        db.Words.AddRange(newWord, oldest, recent);
        db.UserWordProgress.AddRange(
            CreateProgress(userId, oldest.Id, Now.AddDays(-10)),
            CreateProgress(userId, recent.Id, Now.AddDays(-1)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest
            {
                WordCount = 3,
                IncludePreviouslyStudied = true
            },
            TestContext.Current.CancellationToken);

        var itemWordIds = await db.WordStudySessionItems.AsNoTracking()
            .Where(value => value.SessionId == response.Id)
            .OrderBy(value => value.Position)
            .Select(value => value.WordId)
            .ToListAsync(TestContext.Current.CancellationToken);
        itemWordIds.Should().Equal(newWord.Id, oldest.Id, recent.Id);
    }

    /// <summary>
    /// 验证 Random 只选择合规新词、会话内不重复并固定 next 结果。
    /// </summary>
    [Fact]
    public async Task RandomCreationShouldUseEligibleUniqueWordsAndFreezeOrder()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var allStudiedUserId = Guid.NewGuid();
        var eligible = Enumerable.Range(0, 3)
            .Select(index => CreateVisibleWord(
                $"eligible-{index}",
                "en",
                Now.AddMinutes(-index)))
            .ToArray();
        var studied = CreateVisibleWord("studied", "en", Now.AddMinutes(-4));
        db.Words.AddRange([.. eligible, studied]);
        db.UserWordProgress.Add(CreateProgress(userId, studied.Id, Now.AddDays(-1)));
        db.UserWordProgress.AddRange(
            eligible.Append(studied).Select(value =>
                CreateProgress(allStudiedUserId, value.Id, Now.AddDays(-1))));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var session = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest
            {
                WordCount = 3,
                SelectionMode = WordStudySelectionMode.Random
            },
            TestContext.Current.CancellationToken);
        var storedIds = await db.WordStudySessionItems.AsNoTracking()
            .Where(value => value.SessionId == session.Id)
            .OrderBy(value => value.Position)
            .Select(value => value.WordId)
            .ToListAsync(TestContext.Current.CancellationToken);
        var firstNext = await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var repeatedNext = await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var includeStudiedSession = await service.CreateSessionAsync(
            allStudiedUserId,
            new CreateWordStudySessionRequest
            {
                WordCount = 4,
                IncludePreviouslyStudied = true,
                SelectionMode = WordStudySelectionMode.Random
            },
            TestContext.Current.CancellationToken);

        storedIds.Should().OnlyHaveUniqueItems();
        storedIds.Should().BeEquivalentTo(eligible.Select(value => value.Id));
        storedIds.Should().NotContain(studied.Id);
        firstNext.Should().NotBeNull();
        repeatedNext.Should().NotBeNull();
        var firstNextValue = firstNext
            ?? throw new InvalidOperationException("Expected the first random item.");
        var repeatedNextValue = repeatedNext
            ?? throw new InvalidOperationException("Expected the repeated random item.");
        repeatedNextValue.ItemId.Should().Be(firstNextValue.ItemId);
        repeatedNextValue.WordId.Should().Be(firstNextValue.WordId);
        firstNextValue.WordId.Should().Be(storedIds[0]);
        includeStudiedSession.ActualCount.Should().Be(4);
    }

    /// <summary>
    /// 验证同一用户活动会话冲突、不同用户隔离且没有候选时不建空会话。
    /// </summary>
    [Fact]
    public async Task CreationShouldEnforceActiveSessionAndNoEligibleRulesPerUser()
    {
        await using var db = CreateDbContext();
        var firstUserId = Guid.NewGuid();
        var secondUserId = Guid.NewGuid();
        var noEligibleUserId = Guid.NewGuid();
        var word = CreateVisibleWord("only", "en", Now);
        db.Words.Add(word);
        db.UserWordProgress.Add(
            CreateProgress(noEligibleUserId, word.Id, Now.AddDays(-1)));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        await service.CreateSessionAsync(
            firstUserId,
            new CreateWordStudySessionRequest(),
            TestContext.Current.CancellationToken);

        var duplicate = async () => await service.CreateSessionAsync(
            firstUserId,
            new CreateWordStudySessionRequest(),
            TestContext.Current.CancellationToken);
        var otherUser = await service.CreateSessionAsync(
            secondUserId,
            new CreateWordStudySessionRequest(),
            TestContext.Current.CancellationToken);
        var noEligible = async () => await service.CreateSessionAsync(
            noEligibleUserId,
            new CreateWordStudySessionRequest(),
            TestContext.Current.CancellationToken);

        await duplicate.Should().ThrowAsync<ConflictException>();
        otherUser.Status.Should().Be(WordStudySessionStatus.Active);
        await noEligible.Should().ThrowAsync<ConflictException>();
        (await db.WordStudySessions.CountAsync(
            value => value.UserId == noEligibleUserId,
            TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// 验证 Draft、Unpublished 和音频失效词条不会进入初始候选。
    /// </summary>
    [Fact]
    public async Task CreationShouldExcludeNonPublishedAndUnavailableWords()
    {
        await using var db = CreateDbContext();
        var valid = CreateVisibleWord("valid", "en", Now);
        var draft = CreateVisibleWord("draft", "en", Now.AddMinutes(-1));
        var unpublished = CreateVisibleWord(
            "unpublished",
            "en",
            Now.AddMinutes(-2));
        var unavailable = CreateVisibleWord(
            "unavailable-audio",
            "en",
            Now.AddMinutes(-3));
        var archived = CreateVisibleWord(
            "archived",
            "en",
            Now.AddMinutes(-4));
        draft.Status = WordPublicationStatus.Draft;
        draft.PublishedAt = null;
        unpublished.Status = WordPublicationStatus.Unpublished;
        archived.Status = WordPublicationStatus.Archived;
        var unavailableAudio = unavailable.Pronunciations.Single().AudioClip
            ?? throw new InvalidOperationException("Expected pronunciation audio.");
        unavailableAudio.ProcessingStatus = AudioProcessingStatus.Failed;
        db.Words.AddRange(valid, draft, unpublished, unavailable, archived);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var session = await service.CreateSessionAsync(
            Guid.NewGuid(),
            new CreateWordStudySessionRequest { WordCount = 10 },
            TestContext.Current.CancellationToken);
        var selectedWordIds = await db.WordStudySessionItems.AsNoTracking()
            .Where(value => value.SessionId == session.Id)
            .Select(value => value.WordId)
            .ToListAsync(TestContext.Current.CancellationToken);

        session.ActualCount.Should().Be(1);
        selectedWordIds.Should().Equal(valid.Id);
    }

    /// <summary>
    /// 验证 next 跳过失效词条、重复返回同一可见项并在全失效时完成。
    /// </summary>
    [Fact]
    public async Task NextShouldSkipUnavailableItemsAndCompleteWhenNoneRemain()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var first = CreateVisibleWord("first", "en", Now);
        var second = CreateVisibleWord("second", "en", Now.AddMinutes(-1));
        db.Words.AddRange(first, second);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest { WordCount = 2 },
            TestContext.Current.CancellationToken);
        first.Status = WordPublicationStatus.Unpublished;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var next = await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var repeated = await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);

        next.Should().NotBeNull();
        repeated.Should().NotBeNull();
        var nextValue = next
            ?? throw new InvalidOperationException("Expected the available item.");
        var repeatedValue = repeated
            ?? throw new InvalidOperationException("Expected the repeated item.");
        nextValue.WordId.Should().Be(second.Id);
        repeatedValue.ItemId.Should().Be(nextValue.ItemId);
        var firstItem = await db.WordStudySessionItems.AsNoTracking()
            .SingleAsync(
                value => value.SessionId == session.Id &&
                    value.WordId == first.Id,
                TestContext.Current.CancellationToken);
        firstItem.Status.Should().Be(WordStudySessionItemStatus.Skipped);
        firstItem.SkipReason.Should().Be(WordStudySkipReason.ContentUnavailable);
        second.Status = WordPublicationStatus.Unpublished;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var finished = await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var summary = await service.GetSessionAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);

        finished.Should().BeNull();
        summary.Status.Should().Be(WordStudySessionStatus.Completed);
        summary.CompletedCount.Should().Be(2);
        summary.SkippedCount.Should().Be(2);
        (await db.UserWordProgress.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// 验证 Remembered 创建进度、最后一项完成会话且相同结果重试不重复计数。
    /// </summary>
    [Fact]
    public async Task RememberedResultShouldCompleteAndBeIdempotent()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var word = CreateVisibleWord("remember", "en", Now);
        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest { WordCount = 1 },
            TestContext.Current.CancellationToken);
        var item = await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var itemId = item?.ItemId
            ?? throw new InvalidOperationException("Expected a pending study item.");
        var request = new SubmitWordStudyResultRequest
        {
            Result = WordStudyResult.Remembered
        };

        var result = await service.SubmitResultAsync(
            userId,
            session.Id,
            itemId,
            request,
            TestContext.Current.CancellationToken);
        var repeated = await service.SubmitResultAsync(
            userId,
            session.Id,
            itemId,
            request,
            TestContext.Current.CancellationToken);
        var different = async () => await service.SubmitResultAsync(
            userId,
            session.Id,
            itemId,
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Forgotten
            },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(WordStudySessionStatus.Completed);
        repeated.RememberedCount.Should().Be(1);
        var progress = await db.UserWordProgress.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        progress.ReviewCount.Should().Be(1);
        progress.RememberedCount.Should().Be(1);
        progress.ForgottenCount.Should().Be(0);
        progress.FirstStudiedAt.Should().Be(Now);
        progress.LastStudiedAt.Should().Be(Now);
        await different.Should().ThrowAsync<ConflictException>();
        var nextAfterCompletion = async () => await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var abandonAfterCompletion = async () => await service.AbandonSessionAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        await nextAfterCompletion.Should().ThrowAsync<ConflictException>();
        await abandonAfterCompletion.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证后续会话可以对同一词条累计 Forgotten 并保留首次学习时间。
    /// </summary>
    [Fact]
    public async Task LaterSessionShouldAccumulateExistingProgress()
    {
        await using var db = CreateDbContext();
        var clock = new TestTimeProvider(Now);
        var userId = Guid.NewGuid();
        var word = CreateVisibleWord("repeat", "en", Now);
        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db, clock);
        var firstSession = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest { WordCount = 1 },
            TestContext.Current.CancellationToken);
        var firstItem = await service.GetNextItemAsync(
            userId,
            firstSession.Id,
            TestContext.Current.CancellationToken);
        var firstItemId = firstItem?.ItemId
            ?? throw new InvalidOperationException("Expected the first study item.");
        await service.SubmitResultAsync(
            userId,
            firstSession.Id,
            firstItemId,
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Remembered
            },
            TestContext.Current.CancellationToken);
        clock.Advance(TimeSpan.FromDays(1));
        var secondSession = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest
            {
                WordCount = 1,
                IncludePreviouslyStudied = true
            },
            TestContext.Current.CancellationToken);
        var secondItem = await service.GetNextItemAsync(
            userId,
            secondSession.Id,
            TestContext.Current.CancellationToken);
        var secondItemId = secondItem?.ItemId
            ?? throw new InvalidOperationException("Expected the second study item.");

        await service.SubmitResultAsync(
            userId,
            secondSession.Id,
            secondItemId,
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Forgotten
            },
            TestContext.Current.CancellationToken);

        var progress = await db.UserWordProgress.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        progress.ReviewCount.Should().Be(2);
        progress.RememberedCount.Should().Be(1);
        progress.ForgottenCount.Should().Be(1);
        progress.LastResult.Should().Be(WordStudyResult.Forgotten);
        progress.FirstStudiedAt.Should().Be(Now);
        progress.LastStudiedAt.Should().Be(Now.AddDays(1));
    }

    /// <summary>
    /// 验证提交时内容已经失效会 Skip 而不会写入用户进度。
    /// </summary>
    [Fact]
    public async Task ResultShouldSkipContentThatBecameUnavailable()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var word = CreateVisibleWord("unavailable", "en", Now);
        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest { WordCount = 1 },
            TestContext.Current.CancellationToken);
        var itemId = await db.WordStudySessionItems
            .Where(value => value.SessionId == session.Id)
            .Select(value => value.Id)
            .SingleAsync(TestContext.Current.CancellationToken);
        var audio = word.Pronunciations.Single().AudioClip
            ?? throw new InvalidOperationException("Expected pronunciation audio.");
        audio.PublicationStatus =
            AudioPublicationStatus.Unpublished;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await service.SubmitResultAsync(
            userId,
            session.Id,
            itemId,
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Remembered
            },
            TestContext.Current.CancellationToken);

        result.Status.Should().Be(WordStudySessionStatus.Completed);
        result.SkippedCount.Should().Be(1);
        result.RememberedCount.Should().Be(0);
        (await db.UserWordProgress.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// 验证 abandon 幂等，并禁止其他用户访问或继续已放弃会话。
    /// </summary>
    [Fact]
    public async Task AbandonShouldBeIdempotentAndStopFurtherStudy()
    {
        await using var db = CreateDbContext();
        var userId = Guid.NewGuid();
        var otherUserId = Guid.NewGuid();
        var word = CreateVisibleWord("abandon", "en", Now);
        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var session = await service.CreateSessionAsync(
            userId,
            new CreateWordStudySessionRequest { WordCount = 1 },
            TestContext.Current.CancellationToken);
        var itemId = await db.WordStudySessionItems
            .Where(value => value.SessionId == session.Id)
            .Select(value => value.Id)
            .SingleAsync(TestContext.Current.CancellationToken);

        var abandoned = await service.AbandonSessionAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var repeated = await service.AbandonSessionAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var next = async () => await service.GetNextItemAsync(
            userId,
            session.Id,
            TestContext.Current.CancellationToken);
        var submit = async () => await service.SubmitResultAsync(
            userId,
            session.Id,
            itemId,
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Remembered
            },
            TestContext.Current.CancellationToken);
        var crossUser = async () => await service.GetSessionAsync(
            otherUserId,
            session.Id,
            TestContext.Current.CancellationToken);
        var crossUserNext = async () => await service.GetNextItemAsync(
            otherUserId,
            session.Id,
            TestContext.Current.CancellationToken);
        var crossUserSubmit = async () => await service.SubmitResultAsync(
            otherUserId,
            session.Id,
            itemId,
            new SubmitWordStudyResultRequest
            {
                Result = WordStudyResult.Remembered
            },
            TestContext.Current.CancellationToken);

        abandoned.Status.Should().Be(WordStudySessionStatus.Abandoned);
        repeated.AbandonedAt.Should().Be(abandoned.AbandonedAt);
        await next.Should().ThrowAsync<ConflictException>();
        await submit.Should().ThrowAsync<ConflictException>();
        await crossUser.Should().ThrowAsync<NotFoundException>();
        await crossUserNext.Should().ThrowAsync<NotFoundException>();
        await crossUserSubmit.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证两个同时提交的相同结果最终只累计一次进度。
    /// </summary>
    [Fact]
    public async Task ConcurrentSameResultShouldNotDoubleCount()
    {
        var databaseName = Guid.NewGuid().ToString();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        Guid userId = Guid.NewGuid();
        Guid sessionId;
        Guid itemId;
        await using (var setupDb = new ApplicationDbContext(options))
        {
            setupDb.Words.Add(CreateVisibleWord("concurrent", "en", Now));
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            var setupService = CreateService(setupDb);
            var session = await setupService.CreateSessionAsync(
                userId,
                new CreateWordStudySessionRequest { WordCount = 1 },
                TestContext.Current.CancellationToken);
            sessionId = session.Id;
            itemId = await setupDb.WordStudySessionItems
                .Where(value => value.SessionId == sessionId)
                .Select(value => value.Id)
                .SingleAsync(TestContext.Current.CancellationToken);
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var firstService = CreateService(firstDb);
        var secondService = CreateService(secondDb);
        var request = new SubmitWordStudyResultRequest
        {
            Result = WordStudyResult.Remembered
        };

        var results = await Task.WhenAll(
            firstService.SubmitResultAsync(
                userId,
                sessionId,
                itemId,
                request,
                TestContext.Current.CancellationToken),
            secondService.SubmitResultAsync(
                userId,
                sessionId,
                itemId,
                request,
                TestContext.Current.CancellationToken));

        results.Should().OnlyContain(value =>
            value.Status == WordStudySessionStatus.Completed);
        await using var verificationDb = new ApplicationDbContext(options);
        var progress = await verificationDb.UserWordProgress.AsNoTracking()
            .SingleAsync(TestContext.Current.CancellationToken);
        progress.ReviewCount.Should().Be(1);
        progress.RememberedCount.Should().Be(1);
    }

    /// <summary>
    /// 创建使用隔离 InMemory database 的应用上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// 创建使用固定时间和测试替身依赖的背诵服务。
    /// </summary>
    private static WordStudyService CreateService(
        ApplicationDbContext db,
        TimeProvider? timeProvider = null)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            timeProvider ?? new TestTimeProvider(Now),
            Mock.Of<ILogger<WordStudyService>>());

    /// <summary>
    /// 创建一个包含默认发音和例句且满足实时用户可见性规则的词条。
    /// </summary>
    private static Word CreateVisibleWord(
        string headword,
        string languageTag,
        DateTimeOffset publishedAt)
    {
        var source = new MediaResource
        {
            UploaderId = Guid.NewGuid(),
            ObjectName = $"audios/source/{Guid.NewGuid():N}",
            OriginalName = "word.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var audio = new AudioClip
        {
            CreatedById = source.UploaderId,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = headword,
            LanguageTag = languageTag,
            Kind = AudioClipKind.WordPronunciation,
            ProcessingStatus = AudioProcessingStatus.Ready,
            PublicationStatus = AudioPublicationStatus.Published
        };
        var word = new Word
        {
            LanguageTag = languageTag,
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
            Status = WordPublicationStatus.Published,
            PublishedAt = publishedAt,
            CreatedById = Guid.NewGuid(),
            LastEditorId = Guid.NewGuid(),
            Senses =
            [
                new WordSense
                {
                    PartOfSpeech = PartOfSpeech.Noun,
                    Definition = $"definition of {headword}",
                    DefinitionLanguageTag = "en",
                    SortOrder = 0,
                    Examples =
                    [
                        new ExampleSentence
                        {
                            Sentence = $"Use {headword}.",
                            LanguageTag = languageTag,
                            Translation = headword,
                            TranslationLanguageTag = "en",
                            SortOrder = 0
                        }
                    ]
                }
            ],
            Pronunciations =
            [
                new WordPronunciation
                {
                    AudioClipId = audio.Id,
                    AudioClip = audio,
                    IsDefault = true,
                    SortOrder = 0
                }
            ]
        };
        return word;
    }

    /// <summary>
    /// 创建计数一致的单次历史进度用于候选筛选。
    /// </summary>
    private static UserWordProgress CreateProgress(
        Guid userId,
        Guid wordId,
        DateTimeOffset studiedAt)
        => new()
        {
            UserId = userId,
            WordId = wordId,
            ReviewCount = 1,
            RememberedCount = 1,
            LastResult = WordStudyResult.Remembered,
            FirstStudiedAt = studiedAt,
            LastStudiedAt = studiedAt
        };
}
