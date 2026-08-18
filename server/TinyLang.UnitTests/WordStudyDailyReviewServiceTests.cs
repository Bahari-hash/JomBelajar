using FluentAssertions;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class WordStudyDailyReviewServiceTests
{
    [Fact]
    public async Task TodayReviewShouldFilterHiddenWordsAndKeepActivityTypes()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "daily-review@example.test", PasswordHash = "hash" };
        var visible = CreateWord("visible");
        var hidden = CreateWord("hidden");
        hidden.IsDeleted = true;
        db.AddRange(user, visible, hidden);
        db.WordStudyActivities.AddRange(
            CreateActivity(user, visible, WordStudyActivityType.Learning, "2026-08-18T01:00:00Z"),
            CreateActivity(user, visible, WordStudyActivityType.Review, "2026-08-18T02:00:00Z"),
            CreateActivity(user, hidden, WordStudyActivityType.Learning, "2026-08-18T03:00:00Z"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await CreateService(db).GetTodayReviewAsync(
            user.Id,
            new WordStudyTodayReviewRequest { Page = 1, PageSize = 20 },
            TestContext.Current.CancellationToken);

        response.TotalCount.Should().Be(2);
        response.Items.Select(value => value.ActivityType)
            .Should().Equal(WordStudyActivityType.Review, WordStudyActivityType.Learning);
        response.StudyDateUtc.Should().Be(new DateOnly(2026, 8, 18));
    }

    [Fact]
    public async Task CheckInCalendarShouldCalculateUtcStreaksAndMonthRows()
    {
        await using var db = CreateDbContext();
        var user = new User { Email = "check-in@example.test", PasswordHash = "hash" };
        db.Add(user);
        db.WordStudyCheckIns.AddRange(
            CreateCheckIn(user, "2026-08-16", "2026-08-16T01:00:00Z"),
            CreateCheckIn(user, "2026-08-17", "2026-08-17T01:00:00Z"),
            CreateCheckIn(user, "2026-08-18", "2026-08-18T01:00:00Z"),
            CreateCheckIn(user, "2026-08-20", "2026-08-20T01:00:00Z"));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var response = await CreateService(db).GetCheckInCalendarAsync(
            user.Id,
            new WordStudyCheckInCalendarRequest { Year = 2026, Month = 8 },
            TestContext.Current.CancellationToken);

        response.CheckedInDates.Should().HaveCount(4);
        response.CurrentStreak.Should().Be(3);
        response.LongestStreak.Should().Be(3);
        response.TotalCheckInDays.Should().Be(4);
    }

    private static WordStudyActivity CreateActivity(
        User user,
        Word word,
        WordStudyActivityType type,
        string completedAt)
        => new()
        {
            UserId = user.Id,
            WordId = word.Id,
            SessionId = Guid.NewGuid(),
            SessionItemId = Guid.NewGuid(),
            ActivityType = type,
            CompletedAtUtc = DateTimeOffset.Parse(completedAt),
            CreatedAt = DateTimeOffset.Parse(completedAt)
        };

    private static WordStudyCheckIn CreateCheckIn(User user, string date, string checkedAt)
        => new()
        {
            UserId = user.Id,
            StudyDateUtc = DateTimeOffset.Parse($"{date}T00:00:00Z"),
            CheckedInAtUtc = DateTimeOffset.Parse(checkedAt),
            CreatedAt = DateTimeOffset.Parse(checkedAt)
        };

    private static Word CreateWord(string headword)
        => new()
        {
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
            StudyOrder = 1,
            Senses =
            [
                new WordSense
                {
                    PartOfSpeech = PartOfSpeech.Noun,
                    Definition = headword,
                    SortOrder = 0
                }
            ]
        };

    private static WordStudyService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")));

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
