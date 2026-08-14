using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Dtos;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条硬删除不会清除已有的用户学习历史。
/// </summary>
public sealed class WordStudyWordDeletionTests
{
    /// <summary>
    /// 验证 progress 或 session item 引用阻止删除，而无引用 Draft 保持可删除。
    /// </summary>
    [Fact]
    public async Task DeleteShouldRejectStudyHistoryAndKeepUnreferencedBehavior()
    {
        await using var db = new ApplicationDbContext(
            new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options);
        var adminId = Guid.NewGuid();
        var progressWord = CreateDraft("progress", adminId);
        var itemWord = CreateDraft("item", adminId);
        var unreferencedWord = CreateDraft("free", adminId);
        db.Words.AddRange(progressWord, itemWord, unreferencedWord);
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
        db.WordStudySessions.Add(new WordStudySession
        {
            UserId = Guid.NewGuid(),
            RequestedCount = 1,
            ActualCount = 1,
            StartedAt = DateTimeOffset.UtcNow,
            Items =
            [
                new WordStudySessionItem
                {
                    WordId = itemWord.Id,
                    Position = 0
                }
            ]
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new WordService(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System,
            Mock.Of<ILogger<WordService>>());

        var deleteProgressWord = async () => await service.DeleteAsync(
            progressWord.Id,
            adminId,
            new WordMutationRequest
            {
                ConcurrencyStamp = progressWord.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var deleteItemWord = async () => await service.DeleteAsync(
            itemWord.Id,
            adminId,
            new WordMutationRequest { ConcurrencyStamp = itemWord.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        await deleteProgressWord.Should().ThrowAsync<ConflictException>();
        await deleteItemWord.Should().ThrowAsync<ConflictException>();
        await service.DeleteAsync(
            unreferencedWord.Id,
            adminId,
            new WordMutationRequest
            {
                ConcurrencyStamp = unreferencedWord.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        (await db.Words.AnyAsync(
            value => value.Id == progressWord.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await db.Words.AnyAsync(
            value => value.Id == itemWord.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await db.Words.AnyAsync(
            value => value.Id == unreferencedWord.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>
    /// 创建没有私有子项的可删除词条草稿。
    /// </summary>
    private static Word CreateDraft(string headword, Guid adminId)
        => new()
        {
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
            CreatedById = adminId,
            LastEditorId = adminId
        };
}
