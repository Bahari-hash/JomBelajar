using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies the word batch request, response and limit contracts.
/// </summary>
public sealed class WordBatchContractTests
{
    [Fact]
    public void WebJsonDefaultsShouldDeserializeBatchAudioNamesAndPartOfSpeechText()
    {
        const string json = """
            {
              "words": [
                {
                  "headword": "hello",
                  "audioFileName": "hello.mp3",
                  "senses": [
                    {
                      "partOfSpeech": "Interjection",
                      "definition": "A greeting.",
                      "usageNote": "Informal.",
                      "sortOrder": 1,
                      "examples": [
                        {
                          "sentence": "Hello there.",
                          "translation": "Hello.",
                          "audioFileName": "hello-there.mp3",
                          "sortOrder": 2
                        }
                      ]
                    }
                  ]
                }
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<BatchWordRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        request.Should().NotBeNull();
        var word = request!.Words.Should().ContainSingle().Subject;
        word.AudioFileName.Should().Be("hello.mp3");
        var sense = word.Senses.Should().ContainSingle().Subject;
        sense.PartOfSpeech.Should().Be("Interjection");
        sense.Examples.Should().ContainSingle().Which.AudioFileName.Should().Be("hello-there.mp3");
    }

    [Fact]
    public void WebJsonDefaultsShouldRejectNumericBatchPartOfSpeech()
    {
        const string json = """
            {
              "words": [
                {
                  "senses": [
                    {
                      "partOfSpeech": 8
                    }
                  ]
                }
              ]
            }
            """;

        var act = () => JsonSerializer.Deserialize<BatchWordRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        act.Should().Throw<JsonException>();
    }

    [Fact]
    public void WebJsonDefaultsShouldPreserveUnknownBatchPartOfSpeechText()
    {
        const string json = """
            {
              "words": [
                {
                  "senses": [
                    {
                      "partOfSpeech": "UnknownPart"
                    }
                  ]
                }
              ]
            }
            """;

        var request = JsonSerializer.Deserialize<BatchWordRequest>(
            json,
            new JsonSerializerOptions(JsonSerializerDefaults.Web));

        request.Should().NotBeNull();
        request!.Words.Should().ContainSingle()
            .Which.Senses.Should().ContainSingle()
            .Which.PartOfSpeech.Should().Be("UnknownPart");
    }

    [Fact]
    public void BatchInputsShouldExposeOnlyImportContentFields()
    {
        AssertProperties<BatchWordRequest>(
            (nameof(BatchWordRequest.Words), typeof(IReadOnlyCollection<BatchWordRowRequest>)));
        AssertProperties<BatchWordRowRequest>(
            (nameof(BatchWordRowRequest.Headword), typeof(string)),
            (nameof(BatchWordRowRequest.AudioFileName), typeof(string)),
            (nameof(BatchWordRowRequest.Senses), typeof(IReadOnlyCollection<BatchWordSenseInput>)));
        AssertProperties<BatchWordSenseInput>(
            (nameof(BatchWordSenseInput.PartOfSpeech), typeof(string)),
            (nameof(BatchWordSenseInput.Definition), typeof(string)),
            (nameof(BatchWordSenseInput.UsageNote), typeof(string)),
            (nameof(BatchWordSenseInput.SortOrder), typeof(int)),
            (nameof(BatchWordSenseInput.Examples), typeof(IReadOnlyCollection<BatchExampleSentenceInput>)));
        AssertProperties<BatchExampleSentenceInput>(
            (nameof(BatchExampleSentenceInput.Sentence), typeof(string)),
            (nameof(BatchExampleSentenceInput.Translation), typeof(string)),
            (nameof(BatchExampleSentenceInput.AudioFileName), typeof(string)),
            (nameof(BatchExampleSentenceInput.SortOrder), typeof(int)));

        var forbiddenNames = new[]
        {
            "Id",
            "AudioResourceId",
            "ConcurrencyStamp",
            "Status",
            "CreatedAt",
            "UpdatedAt"
        };
        var inputTypes = new[]
        {
            typeof(BatchWordRequest),
            typeof(BatchWordRowRequest),
            typeof(BatchWordSenseInput),
            typeof(BatchExampleSentenceInput)
        };

        inputTypes
            .SelectMany(type => type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            .Select(property => property.Name)
            .Should()
            .NotContain(name => forbiddenNames.Contains(name, StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void BatchPartOfSpeechShouldDeclareExactAllowedValues()
    {
        var attribute = typeof(BatchWordSenseInput)
            .GetProperty(nameof(BatchWordSenseInput.PartOfSpeech))!
            .GetCustomAttribute<AllowedValuesAttribute>();

        attribute.Should().NotBeNull();
        attribute!.Values.Should().Equal(
            "Noun",
            "Verb",
            "Adjective",
            "Adverb",
            "Pronoun",
            "Determiner",
            "Preposition",
            "Conjunction",
            "Interjection",
            "Numeral",
            "Particle",
            "Other");
    }

    [Fact]
    public void BatchLimitsShouldMatchThePublicContract()
    {
        WordConstraints.MaxBatchWordCount.Should().Be(1_000);
        WordConstraints.MaxBatchSenseCount.Should().Be(10_000);
        WordConstraints.MaxBatchExampleCount.Should().Be(50_000);
        WordConstraints.MaxBatchTextCharacterCount.Should().Be(10_000_000);
        WordConstraints.MaxBatchRequestBodyBytes.Should().Be(20L * 1024 * 1024);
    }

    [Fact]
    public void BatchResponsesShouldExposeExactFieldsAndTypes()
    {
        AssertProperties<BatchWordSummaryResponse>(
            ("WordCount", typeof(int)),
            ("SenseCount", typeof(int)),
            ("ExampleCount", typeof(int)),
            ("WordAudioReferenceCount", typeof(int)),
            ("ExampleAudioReferenceCount", typeof(int)),
            ("MatchedAudioReferenceCount", typeof(int)));
        AssertProperties<BatchWordRowValidationResponse>(
            ("RowNumber", typeof(int)),
            ("Headword", typeof(string)),
            ("NormalizedHeadword", typeof(string)),
            ("WordAudioName", typeof(string)),
            ("SenseCount", typeof(int)),
            ("ExampleCount", typeof(int)),
            ("AudioReferenceCount", typeof(int)),
            ("MatchedAudioCount", typeof(int)));
        AssertProperties<BatchWordValidationErrorResponse>(
            ("RowNumber", typeof(int?)),
            ("Field", typeof(string)),
            ("ErrorCode", typeof(ErrorCodes)),
            ("Message", typeof(string)));
        AssertProperties<BatchWordValidationResponse>(
            ("IsValid", typeof(bool)),
            ("Summary", typeof(BatchWordSummaryResponse)),
            ("Rows", typeof(IReadOnlyList<BatchWordRowValidationResponse>)),
            ("Errors", typeof(IReadOnlyList<BatchWordValidationErrorResponse>)));
        AssertProperties<BatchWordCreatedItemResponse>(
            ("RowNumber", typeof(int)),
            ("WordId", typeof(Guid)));
        AssertProperties<BatchWordImportResponse>(
            ("CreatedCount", typeof(int)),
            ("Items", typeof(IReadOnlyList<BatchWordCreatedItemResponse>)));
    }

    [Fact]
    public void BatchErrorCodesShouldExposeStableChineseDescriptions()
    {
        AssertDescription(ErrorCodes.WordBatchRequired, "批量导入至少需要一个词条.");
        AssertDescription(ErrorCodes.WordBatchCountLimit, "批量导入最多支持1000个词条.");
        AssertDescription(ErrorCodes.WordBatchChildCountLimit, "批量导入的释义或例句总数超过限制.");
        AssertDescription(ErrorCodes.WordBatchTextLengthLimit, "批量导入的文本总长度超过限制.");
        AssertDescription(ErrorCodes.WordBatchAudioNotFound, "批量导入引用的音频文件名不存在.");
        AssertDescription(ErrorCodes.WordBatchAudioFailed, "批量导入引用的音频处理失败.");
        AssertDescription(ErrorCodes.WordBatchConflict, "导入期间数据已变更，请重新校验.");
    }

    private static void AssertProperties<T>(params (string Name, Type Type)[] expected)
    {
        typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Select(property => (property.Name, property.PropertyType))
            .Should()
            .Equal(expected);
    }

    private static void AssertDescription(ErrorCodes errorCode, string expected)
    {
        var member = typeof(ErrorCodes).GetMember(errorCode.ToString()).Should().ContainSingle().Subject;
        member.GetCustomAttribute<DescriptionAttribute>()?.Description.Should().Be(expected);
    }
}
