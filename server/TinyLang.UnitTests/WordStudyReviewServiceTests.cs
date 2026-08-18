using FluentAssertions;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class WordStudyReviewServiceTests
{
    [Fact]
    public async Task ReviewShouldSelectOnlyVisibleDueWordsInStablePriorityOrder()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Email = "review-order@example.test",
            PasswordHash = "hash",
            DailyWordReviewCount = 2
        };
        var earlier = CreateWord("earlier", 30);
        var sameDueFirst = CreateWord("same-first", 10);
        var sameDueSecond = CreateWord("same-second", 20);
        var hidden = new Word
        {
            Headword = "hidden",
            NormalizedHeadword = "HIDDEN",
            StudyOrder = 1
        };
        var excluded = CreateWord("excluded", 2);
        db.AddRange(user, earlier, sameDueFirst, sameDueSecond, hidden, excluded);
        AddProgress(db, user, earlier, "2026-08-17T01:00:00Z");
        AddProgress(db, user, sameDueSecond, "2026-08-17T02:00:00Z");
        AddProgress(db, user, sameDueFirst, "2026-08-17T02:00:00Z");
        AddProgress(db, user, hidden, "2026-08-16T01:00:00Z");
        AddProgress(db, user, excluded, "2026-08-15T01:00:00Z", excluded: true);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);
        var overview = await service.GetReviewOverviewAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        var session = await service.StartReviewSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        var selectedIds = await db.WordStudySessionItems
            .Where(value => value.SessionId == session.Id)
            .OrderBy(value => value.Position)
            .Select(value => value.WordId)
            .ToArrayAsync(TestContext.Current.CancellationToken);

        overview.DueCount.Should().Be(3);
        selectedIds.Should().Equal(earlier.Id, sameDueFirst.Id);
    }

    private static void AddProgress(
        ApplicationDbContext db,
        User user,
        Word word,
        string nextReviewAt,
        bool excluded = false)
        => db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = word.Id,
            FirstStudiedAt = DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            LastStudiedAt = DateTimeOffset.Parse("2026-08-01T00:00:00Z"),
            NextReviewAt = excluded ? null : DateTimeOffset.Parse(nextReviewAt),
            IsReviewExcluded = excluded,
            ReviewExcludedAt = excluded
                ? DateTimeOffset.Parse("2026-08-10T00:00:00Z")
                : null
        });

    private static WordStudyService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")));

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
            [
                new WordSense
                {
                    Definition = headword,
                    SortOrder = 0
                }
            ]
        };
}
