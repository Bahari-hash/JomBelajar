using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条硬删除会级联清除直接关联的用户学习记录。
/// </summary>
public sealed class WordStudyWordDeletionTests
{
    [Fact]
    public async Task DeleteShouldCascadeProgressAndSessionItems()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var progressWord = CreateWord("progress");
        var itemWord = CreateWord("item");
        db.Words.AddRange(progressWord, itemWord);
        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = Guid.NewGuid(),
            WordId = progressWord.Id,
            ReviewCount = 1,
            RememberedCount = 1,
            LastResult = WordStudyResult.Remembered,
            FirstStudiedAt = DateTimeOffset.UtcNow,
            LastStudiedAt = DateTimeOffset.UtcNow
        });
        var session = new WordStudySession
        {
            UserId = Guid.NewGuid(),
            RequestedCount = 1,
            ActualCount = 1,
            StudyDateUtc = DateTimeOffset.UtcNow.Date,
            StartedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new WordStudySessionItem
                {
                    WordId = itemWord.Id,
                    Position = 0
                }
            ]
        };
        db.WordStudySessions.Add(session);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new WordService(
            db,
            new PostgresDatabaseExceptionClassifier(),
            NullLogger<WordService>.Instance);

        await service.DeleteAsync(
            progressWord.Id,
            adminId,
            new DeleteWordRequest
            {
                ConcurrencyStamp = progressWord.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        await service.DeleteAsync(
            itemWord.Id,
            adminId,
            new DeleteWordRequest
            {
                ConcurrencyStamp = itemWord.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        (await db.UserWordProgress.AnyAsync(
            value => value.WordId == progressWord.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await db.WordStudySessionItems.AnyAsync(
            value => value.WordId == itemWord.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await db.WordStudySessions.AnyAsync(
            value => value.Id == session.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    private static Word CreateWord(string headword)
        => new()
        {
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
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
}
