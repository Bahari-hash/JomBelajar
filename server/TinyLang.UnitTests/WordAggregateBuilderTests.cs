using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条聚合构建不依赖数据库并保持写入规范化规则。
/// </summary>
public sealed class WordAggregateBuilderTests
{
    [Fact]
    public void CreateShouldNormalizeHeadwordIdentity()
    {
        var word = WordAggregateBuilder.Create(CreateRequest("  Cafe\u0301  "));

        word.Headword.Should().Be("Caf\u00e9");
        word.NormalizedHeadword.Should().Be("CAF\u00c9");
    }

    [Fact]
    public void CreateShouldMapWordSenseAndExampleValues()
    {
        var wordAudioResourceId = Guid.NewGuid();
        var exampleAudioResourceId = Guid.NewGuid();
        var request = new CreateWordRequest
        {
            Headword = "hello",
            AudioResourceId = wordAudioResourceId,
            Senses =
            [
                new WordSenseInput
                {
                    PartOfSpeech = PartOfSpeech.Interjection,
                    Definition = "  a greeting  ",
                    UsageNote = "  informal  ",
                    SortOrder = 3,
                    Examples =
                    [
                        new ExampleSentenceInput
                        {
                            AudioResourceId = exampleAudioResourceId,
                            Sentence = "  Hello there.  ",
                            Translation = "  你好。  ",
                            SortOrder = 4
                        }
                    ]
                }
            ]
        };

        var word = WordAggregateBuilder.Create(request);

        word.AudioResourceId.Should().Be(wordAudioResourceId);
        var sense = word.Senses.Should().ContainSingle().Subject;
        sense.WordId.Should().Be(word.Id);
        sense.Word.Should().BeSameAs(word);
        sense.PartOfSpeech.Should().Be(PartOfSpeech.Interjection);
        sense.Definition.Should().Be("a greeting");
        sense.UsageNote.Should().Be("informal");
        sense.SortOrder.Should().Be(3);
        var example = sense.Examples.Should().ContainSingle().Subject;
        example.WordSenseId.Should().Be(sense.Id);
        example.WordSense.Should().BeSameAs(sense);
        example.AudioResourceId.Should().Be(exampleAudioResourceId);
        example.Sentence.Should().Be("Hello there.");
        example.Translation.Should().Be("你好。");
        example.SortOrder.Should().Be(4);
    }

    [Fact]
    public void CreateShouldRejectBlankHeadword()
    {
        var action = () => WordAggregateBuilder.Create(CreateRequest("  \t  "));

        action.Should().Throw<RequestValidationException>()
            .Which.ErrorCode.Should().Be(ErrorCodes.WordHeadwordRequired);
    }

    [Fact]
    public void CreateShouldRejectHeadwordExceedingNormalizedLengthLimit()
    {
        var headword = $"  {new string('a', WordConstraints.MaxHeadwordLength + 1)}  ";

        var action = () => WordAggregateBuilder.Create(CreateRequest(headword));

        action.Should().Throw<RequestValidationException>()
            .Which.ErrorCode.Should().Be(ErrorCodes.WordHeadwordLengthLimit);
    }

    private static CreateWordRequest CreateRequest(string headword)
        => new()
        {
            Headword = headword,
            Senses = []
        };
}
