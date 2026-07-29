using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Entities;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条 EF model 中的关键唯一索引、partial filter 和删除行为。
/// </summary>
public sealed class WordModelTests
{
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
                new[] { nameof(Word.LanguageTag), nameof(Word.NormalizedHeadword) }));
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
        example!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(value => value.Name).Contains(nameof(ExampleSentence.SortOrder)));
    }

    /// <summary>
    /// 验证私有子项级联删除而 AudioClip 引用使用 Restrict。
    /// </summary>
    [Fact]
    public void ModelShouldUseCascadeForChildrenAndRestrictForAudio()
    {
        using var db = CreateDbContext();
        var pronunciation = db.Model.FindEntityType(typeof(WordPronunciation))!;
        var example = db.Model.FindEntityType(typeof(ExampleSentence))!;

        pronunciation.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        pronunciation.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(AudioClip))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
        example.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(WordSense))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        example.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(AudioClip))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    /// <summary>
    /// 创建仅用于读取 EF model 的隔离上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
