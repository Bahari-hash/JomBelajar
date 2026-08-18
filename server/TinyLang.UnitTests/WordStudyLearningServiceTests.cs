using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class WordStudyLearningServiceTests
{
    [Fact]
    public async Task LearningShouldSelectByStudyOrderAndResumeActiveSession()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Email = "learning@example.test",
            PasswordHash = "hash",
            DailyWordStudyCount = 2
        };
        var first = CreateWord("first", 10);
        var second = CreateWord("second", 20);
        var third = CreateWord("third", 30);
        db.AddRange(user, first, second, third);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        var repeated = await service.StartLearningSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);

        session.CurrentItem!.WordId.Should().Be(first.Id);
        repeated.Id.Should().Be(session.Id);
        repeated.CurrentItem!.WordId.Should().Be(first.Id);
    }

    [Fact]
    public async Task LearningShouldExcludeExistingProgressAndReportNoMoreWords()
    {
        await using var db = CreateDbContext();
        var user = new User
        {
            Email = "complete@example.test",
            PasswordHash = "hash",
            DailyWordStudyCount = 10
        };
        var studied = CreateWord("studied", 1);
        var available = CreateWord("available", 2);
        db.AddRange(user, studied, available);
        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = studied.Id,
            FirstStudiedAt = DateTimeOffset.UtcNow,
            LastStudiedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var service = CreateService(db);
        var session = await service.StartLearningSessionAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        session.CurrentItem!.WordId.Should().Be(available.Id);

        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = available.Id,
            FirstStudiedAt = DateTimeOffset.UtcNow,
            LastStudiedAt = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var overview = await service.GetLearningOverviewAsync(
            user.Id,
            TestContext.Current.CancellationToken);
        overview.HasMoreWords.Should().BeFalse();
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

    private static Word CreateWord(string headword, long studyOrder)
        => new()
        {
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
            StudyOrder = studyOrder,
            Senses =
            [
                new WordSense
                {
                    PartOfSpeech = PartOfSpeech.Noun,
                    Definition = $"definition {headword}",
                    SortOrder = 0
                }
            ]
        };
}
