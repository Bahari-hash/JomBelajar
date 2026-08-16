using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条 EF model 中的关键唯一索引、partial filter 和删除行为。
/// </summary>
public sealed class WordModelTests
{
    [Fact]
    public void WordTextModelsShouldNotExposeAudioClipReferences()
    {
        typeof(WordPronunciation).GetProperty("AudioClipId").Should().BeNull();
        typeof(WordPronunciation).GetProperty("AudioClip").Should().BeNull();
        typeof(ExampleSentence).GetProperty("AudioClipId").Should().BeNull();
        typeof(ExampleSentence).GetProperty("AudioClip").Should().BeNull();
        typeof(WordPronunciationInput).GetProperty("AudioClipId").Should().BeNull();
        typeof(ExampleSentenceInput).GetProperty("AudioClipId").Should().BeNull();
        typeof(AdminWordPronunciationResponse).GetProperty("AudioClipId").Should().BeNull();
        typeof(AdminExampleSentenceResponse).GetProperty("AudioClipId").Should().BeNull();
        typeof(WordPronunciationResponse).GetProperty("AudioClipId").Should().BeNull();
        typeof(ExampleSentenceResponse).GetProperty("AudioClipId").Should().BeNull();
    }

    /// <summary>
    /// 验证规范化词头、排序和默认发音约束已进入 EF model。
    /// </summary>
    [Fact]
    public void ModelShouldContainWordUniqueAndPartialIndexes()
    {
        using var db = CreateDbContext();
        var word = db.Model.FindEntityType(typeof(Word));
        var pronunciation = db.Model.FindEntityType(typeof(WordPronunciation));
        var sense = db.Model.FindEntityType(typeof(WordSense));
        var example = db.Model.FindEntityType(typeof(ExampleSentence));

        word.Should().NotBeNull();
        word!.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { nameof(Word.NormalizedHeadword) }));
        pronunciation!.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.GetFilter() == "\"IsDefault\" = TRUE");
        pronunciation.GetIndexes().Should().Contain(index =>
            index.IsUnique && index.Properties.Select(value => value.Name).SequenceEqual(
                new[]
                {
                    nameof(WordPronunciation.WordId),
                    nameof(WordPronunciation.SortOrder)
                }));
        sense!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(value => value.Name).Contains(nameof(WordSense.SortOrder)));
        sense.GetIndexes().Should().Contain(index =>
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { nameof(WordSense.PartOfSpeech), nameof(WordSense.WordId) }));
        example!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(value => value.Name).Contains(nameof(ExampleSentence.SortOrder)));
    }

    /// <summary>
    /// 验证归档状态和归档时间进入可空实体模型且状态枚举包含终态。
    /// </summary>
    [Fact]
    public void ModelShouldContainArchivedStateAndTimestamp()
    {
        using var db = CreateDbContext();
        var word = db.Model.FindEntityType(typeof(Word));

        Enum.IsDefined(WordPublicationStatus.Archived).Should().BeTrue();
        word.Should().NotBeNull();
        var archivedAt = word!.FindProperty(nameof(Word.ArchivedAt));
        archivedAt.Should().NotBeNull();
        archivedAt!.IsNullable.Should().BeTrue();
    }

    /// <summary>
    /// 验证私有子项保留级联删除且不再存在 AudioClip 外键。
    /// </summary>
    [Fact]
    public void ModelShouldUseCascadeForChildrenWithoutAudioForeignKeys()
    {
        using var db = CreateDbContext();
        var pronunciation = db.Model.FindEntityType(typeof(WordPronunciation))!;
        var example = db.Model.FindEntityType(typeof(ExampleSentence))!;

        pronunciation.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        pronunciation.GetForeignKeys().Should().NotContain(value =>
            value.PrincipalEntityType.ClrType.FullName == "TinyLang.Entities.AudioClip");
        example.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(WordSense))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        example.GetForeignKeys().Should().NotContain(value =>
            value.PrincipalEntityType.ClrType.FullName == "TinyLang.Entities.AudioClip");
    }

    /// <summary>
    /// 创建仅用于读取 EF model 的隔离上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
