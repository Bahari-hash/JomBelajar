using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Entities;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证重构后词条 EF model 的唯一索引、共享音频关系和删除行为。
/// </summary>
public sealed class WordModelTests
{
    [Fact]
    public void WordShouldExposeSingleOptionalAudioReferenceWithoutLifecycleFields()
    {
        var wordType = typeof(Word);

        wordType.GetProperty("AudioResourceId").Should().NotBeNull();
        wordType.GetProperty("AudioResource").Should().NotBeNull();
        wordType.GetProperty("Status").Should().BeNull();
        wordType.GetProperty("PublishedAt").Should().BeNull();
        wordType.GetProperty("ArchivedAt").Should().BeNull();
        wordType.GetProperty("CreatedById").Should().BeNull();
        wordType.GetProperty("CreatedBy").Should().BeNull();
        wordType.GetProperty("LastEditorId").Should().BeNull();
        wordType.GetProperty("LastEditor").Should().BeNull();
        wordType.GetProperty("Pronunciations").Should().BeNull();
    }

    [Fact]
    public void ExampleSentenceShouldExposeOptionalAudioReference()
    {
        var exampleType = typeof(ExampleSentence);

        exampleType.GetProperty("AudioResourceId").Should().NotBeNull();
        exampleType.GetProperty("AudioResource").Should().NotBeNull();
    }

    [Fact]
    public void ModelShouldContainWordUniqueAndAudioIndexes()
    {
        using var db = CreateDbContext();
        var word = db.Model.FindEntityType(typeof(Word));
        var sense = db.Model.FindEntityType(typeof(WordSense));
        var example = db.Model.FindEntityType(typeof(ExampleSentence));

        word.Should().NotBeNull();
        word!.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { nameof(Word.NormalizedHeadword) }));
        word.GetIndexes().Should().Contain(index =>
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { "AudioResourceId" }));
        word.FindProperty("AudioResourceId")!.IsNullable.Should().BeTrue();
        word.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(AudioResource))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);

        sense!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(value => value.Name).Contains(nameof(WordSense.SortOrder)));
        sense.GetIndexes().Should().Contain(index =>
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { nameof(WordSense.PartOfSpeech), nameof(WordSense.WordId) }));
        example!.GetIndexes().Should().Contain(index => index.IsUnique &&
            index.Properties.Select(value => value.Name).Contains(
                nameof(ExampleSentence.SortOrder)));
        example.GetIndexes().Should().Contain(index =>
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { "AudioResourceId" }));
        example.FindProperty("AudioResourceId")!.IsNullable.Should().BeTrue();
        example.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(AudioResource))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void RetiredPronunciationModelShouldBeAbsent()
    {
        using var db = CreateDbContext();

        typeof(Word).Assembly.GetType("TinyLang.Entities.WordPronunciation")
            .Should().BeNull();
        db.Model.FindEntityType("TinyLang.Entities.WordPronunciation")
            .Should().BeNull();
        typeof(ApplicationDbContext).GetProperty("WordPronunciations")
            .Should().BeNull();
    }

    [Fact]
    public void ModelShouldCascadeWordChildrenAndStudyReferences()
    {
        using var db = CreateDbContext();
        var sense = db.Model.FindEntityType(typeof(WordSense))!;
        var example = db.Model.FindEntityType(typeof(ExampleSentence))!;
        var progress = db.Model.FindEntityType(typeof(UserWordProgress))!;
        var studyItem = db.Model.FindEntityType(typeof(WordStudySessionItem))!;

        sense.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        example.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(WordSense))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        progress.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
        studyItem.GetForeignKeys().Single(value =>
            value.PrincipalEntityType.ClrType == typeof(Word))
            .DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
