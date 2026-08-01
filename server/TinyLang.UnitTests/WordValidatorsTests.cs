using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条创建、完整更新、嵌套输入和列表筛选边界。
/// </summary>
public sealed class WordValidatorsTests
{
    /// <summary>
    /// 验证 Draft 可以使用空子集合，同时保留合法词头和语言要求。
    /// </summary>
    [Fact]
    public async Task EmptyDraftShouldBeValid()
    {
        var result = await new CreateWordRequestValidator().ValidateAsync(
            new CreateWordRequest { Headword = "hello", LanguageTag = "en-US" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证创建请求不能指定服务端子项标识。
    /// </summary>
    [Fact]
    public async Task CreateShouldRejectClientChildIds()
    {
        var request = CreateValidRequest() with
        {
            Senses =
            [
                CreateValidRequest().Senses.Single() with { Id = Guid.NewGuid() }
            ]
        };

        var result = await new CreateWordRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证重复排序、发音音频、默认项和空并发标识均被拒绝。
    /// </summary>
    [Fact]
    public async Task UpdateShouldRejectDuplicateTargetsAndEmptyConcurrencyStamp()
    {
        var audioId = Guid.NewGuid();
        var request = new UpdateWordRequest
        {
            Headword = "hello",
            LanguageTag = "en",
            Senses =
            [
                CreateSense(0),
                CreateSense(0)
            ],
            Pronunciations =
            [
                new WordPronunciationInput
                {
                    AudioClipId = audioId,
                    IsDefault = true,
                    SortOrder = 0
                },
                new WordPronunciationInput
                {
                    AudioClipId = audioId,
                    IsDefault = true,
                    SortOrder = 0
                }
            ]
        };

        var result = await new UpdateWordRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(UpdateWordRequest.ConcurrencyStamp));
    }

    /// <summary>
    /// 验证嵌套必填文本、语言、词性、音频和排序范围。
    /// </summary>
    [Fact]
    public async Task NestedInputsShouldRejectInvalidFields()
    {
        var request = new CreateWordRequest
        {
            Headword = "word",
            LanguageTag = "invalid_tag",
            Senses =
            [
                new WordSenseInput
                {
                    PartOfSpeech = (PartOfSpeech)999,
                    Definition = string.Empty,
                    DefinitionLanguageTag = "bad_tag",
                    SortOrder = -1,
                    Examples =
                    [
                        new ExampleSentenceInput
                        {
                            Sentence = string.Empty,
                            LanguageTag = "bad_tag",
                            Translation = string.Empty,
                            TranslationLanguageTag = "bad_tag",
                            SortOrder = -1
                        }
                    ]
                }
            ],
            Pronunciations =
            [
                new WordPronunciationInput
                {
                    AudioClipId = Guid.Empty,
                    SortOrder = -1
                }
            ]
        };

        var result = await new CreateWordRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(5);
    }

    /// <summary>
    /// 验证两类列表请求拒绝越界分页、非法语言和状态枚举。
    /// </summary>
    [Fact]
    public async Task ListsShouldRejectInvalidPagingLanguageAndStatus()
    {
        var adminResult = await new AdminWordListRequestValidator().ValidateAsync(
            new AdminWordListRequest
            {
                Page = 0,
                PageSize = 101,
                Language = "bad_tag",
                Status = (WordPublicationStatus)999
            },
            TestContext.Current.CancellationToken);
        var userResult = await new WordListRequestValidator().ValidateAsync(
            new WordListRequest { Page = 0, PageSize = 101, Language = "bad_tag" },
            TestContext.Current.CancellationToken);

        adminResult.IsValid.Should().BeFalse();
        userResult.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证管理员定义筛选拒绝空白和控制字符，并接受合法词性。
    /// </summary>
    [Fact]
    public async Task AdminListShouldValidateDefinitionFilter()
    {
        var validator = new AdminWordListRequestValidator();

        var blank = await validator.ValidateAsync(
            new AdminWordListRequest { Definition = "   " },
            TestContext.Current.CancellationToken);
        var control = await validator.ValidateAsync(
            new AdminWordListRequest { Definition = "meaning\n" },
            TestContext.Current.CancellationToken);
        var valid = await validator.ValidateAsync(
            new AdminWordListRequest
            {
                Definition = "meaning",
                PartOfSpeech = PartOfSpeech.Noun
            },
            TestContext.Current.CancellationToken);

        blank.IsValid.Should().BeFalse();
        control.IsValid.Should().BeFalse();
        valid.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证状态动作请求必须携带非空并发标识。
    /// </summary>
    [Fact]
    public async Task MutationShouldRequireConcurrencyStamp()
    {
        var result = await new WordMutationRequestValidator().ValidateAsync(
            new WordMutationRequest(),
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(WordMutationRequest.ConcurrencyStamp));
    }

    /// <summary>
    /// 创建包含一个释义、例句和默认发音的有效请求。
    /// </summary>
    private static CreateWordRequest CreateValidRequest()
        => new()
        {
            Headword = "hello",
            LanguageTag = "en",
            Senses = [CreateSense(0)],
            Pronunciations =
            [
                new WordPronunciationInput
                {
                    AudioClipId = Guid.NewGuid(),
                    IsDefault = true,
                    SortOrder = 0
                }
            ]
        };

    /// <summary>
    /// 创建指定排序值的有效释义和例句输入。
    /// </summary>
    private static WordSenseInput CreateSense(int sortOrder)
        => new()
        {
            PartOfSpeech = PartOfSpeech.Noun,
            Definition = "a greeting",
            DefinitionLanguageTag = "en",
            SortOrder = sortOrder,
            Examples =
            [
                new ExampleSentenceInput
                {
                    Sentence = "Hello there.",
                    LanguageTag = "en",
                    Translation = "你好。",
                    TranslationLanguageTag = "zh-CN",
                    SortOrder = 0
                }
            ]
        };
}
