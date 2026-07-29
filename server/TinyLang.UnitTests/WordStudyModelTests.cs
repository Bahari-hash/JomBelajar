using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Entities;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证单词背诵 EF model 的唯一索引、并发标识和删除边界。
/// </summary>
public sealed class WordStudyModelTests
{
    /// <summary>
    /// 验证 progress、Active session 和固定 item 的关键唯一约束。
    /// </summary>
    [Fact]
    public void ModelShouldContainStudyUniqueAndPartialIndexes()
    {
        using var db = CreateDbContext();
        var progress = db.Model.FindEntityType(typeof(UserWordProgress))
            ?? throw new InvalidOperationException("Progress model is missing.");
        var session = db.Model.FindEntityType(typeof(WordStudySession))
            ?? throw new InvalidOperationException("Session model is missing.");
        var item = db.Model.FindEntityType(typeof(WordStudySessionItem))
            ?? throw new InvalidOperationException("Session item model is missing.");

        progress.GetIndexes().Should().Contain(index =>
            index.IsUnique && PropertiesEqual(
                index.Properties.Select(value => value.Name),
                nameof(UserWordProgress.UserId),
                nameof(UserWordProgress.WordId)));
        session.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.GetFilter() == "\"Status\" = 'Active'" &&
            PropertiesEqual(
                index.Properties.Select(value => value.Name),
                nameof(WordStudySession.UserId)));
        item.GetIndexes().Should().Contain(index =>
            index.IsUnique && PropertiesEqual(
                index.Properties.Select(value => value.Name),
                nameof(WordStudySessionItem.SessionId),
                nameof(WordStudySessionItem.WordId)));
        item.GetIndexes().Should().Contain(index =>
            index.IsUnique && PropertiesEqual(
                index.Properties.Select(value => value.Name),
                nameof(WordStudySessionItem.SessionId),
                nameof(WordStudySessionItem.Position)));
        (session.FindProperty(nameof(WordStudySession.ConcurrencyStamp))
            ?? throw new InvalidOperationException("Session stamp is missing."))
            .IsConcurrencyToken.Should().BeTrue();
        (item.FindProperty(nameof(WordStudySessionItem.ConcurrencyStamp))
            ?? throw new InvalidOperationException("Item stamp is missing."))
            .IsConcurrencyToken.Should().BeTrue();
    }

    /// <summary>
    /// 验证 User/Session 级联清理而 Word 学习历史使用 Restrict。
    /// </summary>
    [Fact]
    public void ModelShouldUseExpectedStudyDeleteBehaviors()
    {
        using var db = CreateDbContext();
        var progress = db.Model.FindEntityType(typeof(UserWordProgress))
            ?? throw new InvalidOperationException("Progress model is missing.");
        var session = db.Model.FindEntityType(typeof(WordStudySession))
            ?? throw new InvalidOperationException("Session model is missing.");
        var item = db.Model.FindEntityType(typeof(WordStudySessionItem))
            ?? throw new InvalidOperationException("Session item model is missing.");

        progress.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(User))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        progress.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        session.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(User))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        item.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(WordStudySession))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        item.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// 判断索引属性序列是否与预期顺序完全一致。
    /// </summary>
    private static bool PropertiesEqual(
        IEnumerable<string> actual,
        params string[] expected)
        => actual.SequenceEqual(expected);

    /// <summary>
    /// 创建仅用于读取 EF model 的隔离上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
