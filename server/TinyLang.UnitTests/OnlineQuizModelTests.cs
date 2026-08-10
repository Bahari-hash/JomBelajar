using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using TinyLang.Database;
using TinyLang.Entities;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证在线试卷模型的关键唯一性、并发和历史删除边界 metadata。
/// </summary>
public sealed class OnlineQuizModelTests
{
    /// <summary>
    /// 验证活动测验 partial unique、答案唯一和并发标识配置。
    /// </summary>
    [Fact]
    public void ModelShouldConfigureAttemptUniquenessAndConcurrency()
    {
        using var db = CreateDbContext();
        var attemptType = db.Model.FindEntityType(typeof(PaperAttempt))!;
        var activeIndex = attemptType.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name)
                .SequenceEqual([nameof(PaperAttempt.UserId), nameof(PaperAttempt.PaperId)]));
        var answerType = db.Model.FindEntityType(typeof(PaperAttemptAnswer))!;

        activeIndex.IsUnique.Should().BeTrue();
        activeIndex.GetFilter().Should().Be("\"Status\" = 'InProgress'");
        attemptType.FindProperty(nameof(PaperAttempt.ConcurrencyStamp))!
            .IsConcurrencyToken.Should().BeTrue();
        answerType.FindProperty(nameof(PaperAttemptAnswer.ConcurrencyStamp))!
            .IsConcurrencyToken.Should().BeTrue();
        answerType.GetIndexes().Single(index =>
            index.Properties.Select(property => property.Name).SequenceEqual(
                [nameof(PaperAttemptAnswer.AttemptId),
                 nameof(PaperAttemptAnswer.QuestionId)])).IsUnique.Should().BeTrue();
    }

    /// <summary>
    /// 验证编辑聚合私有子项级联而成绩到内容的关系使用 Restrict。
    /// </summary>
    [Fact]
    public void ModelShouldProtectAttemptHistoryFromContentDeletion()
    {
        using var db = CreateDbContext();
        var questionType = db.Model.FindEntityType(typeof(PaperQuestion))!;
        var attemptType = db.Model.FindEntityType(typeof(PaperAttempt))!;
        var answerType = db.Model.FindEntityType(typeof(PaperAttemptAnswer))!;

        questionType.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Paper))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        attemptType.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Paper))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        answerType.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(PaperQuestion))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        answerType.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(PaperQuestionOption))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// 验证试卷模型包含可空归档时间且状态仍按字符串持久化。
    /// </summary>
    [Fact]
    public void ModelShouldConfigureArchivedPaperState()
    {
        using var db = CreateDbContext();
        var paperType = db.Model.FindEntityType(typeof(Paper))!;

        paperType.FindProperty(nameof(Paper.ArchivedAt))!.IsNullable.Should().BeTrue();
        paperType.FindProperty(nameof(Paper.Status))!.GetProviderClrType()
            .Should().Be<string>();
    }
    /// <summary>
    /// 验证试卷标签映射为必填 PostgreSQL text[]，并以空数组作为默认值。
    /// </summary>
    [Fact]
    public void ModelShouldConfigurePaperTagsAsRequiredTextArray()
    {
        using var db = CreateDbContext();
        var tags = db.Model.FindEntityType(typeof(Paper))!
            .FindProperty(nameof(Paper.Tags))!;

        tags.IsNullable.Should().BeFalse();
        tags.FindAnnotation(RelationalAnnotationNames.ColumnType)!.Value
            .Should().Be("text[]");
        tags.FindAnnotation(RelationalAnnotationNames.DefaultValueSql)!.Value
            .Should().Be("'{}'::text[]");
    }

    /// <summary>
    /// 创建使用隔离 InMemory 数据库的应用上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
