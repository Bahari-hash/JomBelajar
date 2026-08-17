using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条创建、完整更新、嵌套输入和列表筛选边界。
/// </summary>
public sealed class WordValidatorsTests
{
    [Fact]
    public async Task WordShouldRequireAtLeastOneSense()
    {
        var result = await new CreateWordRequestValidator().ValidateAsync(
            new CreateWordRequest { Headword = "hello" },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.ErrorCode == "WordSenseRequired");
    }

    [Fact]
    public async Task SenseWithoutExamplesShouldBeValid()
    {
        var result = await new CreateWordRequestValidator().ValidateAsync(
            new CreateWordRequest
            {
                Headword = "hello",
                Senses =
                [
                    new WordSenseInput
                    {
                        PartOfSpeech = PartOfSpeech.Noun,
                        Definition = "a greeting",
                        SortOrder = 0,
                        Examples = []
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void WordContractsShouldExposeOptionalSharedAudioReference()
    {
        typeof(WordUpsertRequest).GetProperty("AudioResourceId")
            .Should().NotBeNull();
        Enum.TryParse<ErrorCodes>("WordAudioInvalid", out _)
            .Should().BeTrue();
    }

    [Fact]
    public void ExampleContractsShouldExposeOptionalSharedAudioReference()
    {
        typeof(ExampleSentenceInput).GetProperty("AudioResourceId")
            .Should().NotBeNull();
        typeof(ExampleSentenceResponse).GetProperty("AudioResourceId")
            .Should().NotBeNull();
        typeof(AdminExampleSentenceResponse).GetProperty("Audio")
            .Should().NotBeNull();
        typeof(ExampleSentenceInput).Assembly
            .GetType("TinyLang.Dtos.AdminExampleSentenceAudioResponse")
            .Should().NotBeNull();
        Enum.TryParse<ErrorCodes>("WordExampleAudioInvalid", out _)
            .Should().BeTrue();
    }

    [Fact]
    public async Task ExampleAudioReferenceShouldAllowNull()
    {
        var result = await new ExampleSentenceInputValidator().ValidateAsync(
            new ExampleSentenceInput
            {
                AudioResourceId = null,
                Sentence = "Hello there.",
                Translation = "你好。",
                SortOrder = 0
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task ExampleAudioReferenceShouldRejectEmptyId()
    {
        var result = await new ExampleSentenceInputValidator().ValidateAsync(
            new ExampleSentenceInput
            {
                AudioResourceId = Guid.Empty,
                Sentence = "Hello there.",
                Translation = "你好。",
                SortOrder = 0
            },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().ContainSingle(error =>
            error.PropertyName == nameof(ExampleSentenceInput.AudioResourceId) &&
            error.ErrorCode == nameof(ErrorCodes.WordExampleAudioInvalid));
    }

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

    [Fact]
    public async Task UpdateShouldRejectDuplicateTargetsAndEmptyConcurrencyStamp()
    {
        var request = new UpdateWordRequest
        {
            Headword = "hello",
            Senses =
            [
                CreateSense(0),
                CreateSense(0)
            ]
        };

        var result = await new UpdateWordRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(error =>
            error.PropertyName == nameof(UpdateWordRequest.ConcurrencyStamp));
    }

    [Fact]
    public async Task NestedInputsShouldRejectInvalidFields()
    {
        var request = new CreateWordRequest
        {
            Headword = "word",
            Senses =
            [
                new WordSenseInput
                {
                    PartOfSpeech = (PartOfSpeech)999,
                    Definition = string.Empty,
                    SortOrder = -1,
                    Examples =
                    [
                        new ExampleSentenceInput
                        {
                            Sentence = string.Empty,
                            Translation = string.Empty,
                            SortOrder = -1
                        }
                    ]
                }
            ]
        };

        var result = await new CreateWordRequestValidator().ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCountGreaterThan(4);
    }

    [Fact]
    public async Task ListsShouldRejectInvalidPagingAndPartOfSpeech()
    {
        var adminResult = await new AdminWordListRequestValidator().ValidateAsync(
            new AdminWordListRequest
            {
                Page = 0,
                PageSize = 101,
                PartOfSpeech = (PartOfSpeech)999
            },
            TestContext.Current.CancellationToken);
        var userResult = await new WordListRequestValidator().ValidateAsync(
            new WordListRequest { Page = 0, PageSize = 101 },
            TestContext.Current.CancellationToken);

        adminResult.IsValid.Should().BeFalse();
        userResult.IsValid.Should().BeFalse();
    }

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

    [Fact]
    public void DeleteContractShouldUseDedicatedConcurrencyRequest()
    {
        var contractAssembly = typeof(CreateWordRequest).Assembly;

        contractAssembly.GetType("TinyLang.Dtos.DeleteWordRequest")
            .Should().NotBeNull();
        contractAssembly.GetType("TinyLang.Dtos.WordMutationRequest")
            .Should().BeNull();
    }

    private static CreateWordRequest CreateValidRequest()
        => new()
        {
            Headword = "hello",
            Senses = [CreateSense(0)]
        };

    private static WordSenseInput CreateSense(int sortOrder)
        => new()
        {
            PartOfSpeech = PartOfSpeech.Noun,
            Definition = "a greeting",
            SortOrder = sortOrder,
            Examples =
            [
                new ExampleSentenceInput
                {
                    Sentence = "Hello there.",
                    Translation = "你好。",
                    SortOrder = 0
                }
            ]
        };
}
