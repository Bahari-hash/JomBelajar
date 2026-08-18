using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证单词背诵 EF model 的唯一索引、并发标识和删除边界。
/// </summary>
public sealed class WordStudyModelTests
{
    [Fact]
    public void NewStudyEntitiesShouldUseExpectedDefaults()
    {
        var user = new User
        {
            Email = "learner@example.com",
            PasswordHash = "hash"
        };
        var session = new WordStudySession();
        var item = new WordStudySessionItem();
        var favorite = new UserWordFavorite();

        user.DailyWordReviewCount.Should().Be(50);
        session.SessionType.Should().Be(WordStudySessionType.Learning);
        session.Phase.Should().Be(WordStudyPhase.Memorization);
        session.Status.Should().Be(WordStudySessionStatus.Active);
        item.Status.Should().Be(WordStudySessionItemStatus.Pending);
        item.ConcurrencyStamp.Should().NotBeEmpty();
        favorite.Id.Should().NotBeEmpty();
    }

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
        session.GetIndexes().Count(index =>
                index.IsUnique &&
                index.GetFilter() is not null &&
                PropertiesEqual(
                    index.Properties.Select(value => value.Name),
                    nameof(WordStudySession.UserId)))
            .Should().Be(2);
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

    [Fact]
    public void ModelShouldConfigureUpgradedStudyPersistence()
    {
        using var db = CreateDbContext();
        var model = db.GetService<IDesignTimeModel>().Model;
        var word = model.FindEntityType(typeof(Word))
            ?? throw new InvalidOperationException("Word model is missing.");
        var user = model.FindEntityType(typeof(User))
            ?? throw new InvalidOperationException("User model is missing.");
        var progress = model.FindEntityType(typeof(UserWordProgress))
            ?? throw new InvalidOperationException("Progress model is missing.");
        var session = model.FindEntityType(typeof(WordStudySession))
            ?? throw new InvalidOperationException("Session model is missing.");
        var item = model.FindEntityType(typeof(WordStudySessionItem))
            ?? throw new InvalidOperationException("Session item model is missing.");
        var favorite = model.FindEntityType(typeof(UserWordFavorite))
            ?? throw new InvalidOperationException("Favorite model is missing.");

        word.FindProperty(nameof(Word.StudyOrder))!.ValueGenerated
            .Should().Be(ValueGenerated.OnAdd);
        word.GetIndexes().Should().Contain(index =>
            index.IsUnique && PropertiesEqual(
                index.Properties.Select(value => value.Name),
                nameof(Word.StudyOrder)));
        progress.GetIndexes().Should().Contain(index => PropertiesEqual(
            index.Properties.Select(value => value.Name),
            nameof(UserWordProgress.UserId),
            nameof(UserWordProgress.NextReviewAt),
            nameof(UserWordProgress.WordId)));
        session.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.GetFilter() ==
                "\"Status\" = 'Active' AND \"SessionType\" = 'Learning'");
        session.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.GetFilter() ==
                "\"Status\" = 'Active' AND \"SessionType\" = 'Review'");
        favorite.GetIndexes().Should().ContainSingle(index => index.IsUnique);

        user.GetCheckConstraints().Select(value => value.Name)
            .Should().Contain("CK_users_daily_word_review_count");
        progress.GetCheckConstraints().Select(value => value.Name)
            .Should().Contain([
                "CK_user_word_progress_review_stage",
                "CK_user_word_progress_review_counts"
            ]);
        item.GetCheckConstraints().Select(value => value.Name)
            .Should().Contain("CK_word_study_session_items_attempt_counts");
    }

    /// <summary>
    /// 验证用户、会话和单词删除都会级联清理直接关联的学习记录。
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
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        session.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(User))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        item.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(WordStudySession))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        item.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
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
