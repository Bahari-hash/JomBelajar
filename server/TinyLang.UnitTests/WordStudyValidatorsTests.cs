using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证双阶段单词学习命令的输入边界。
/// </summary>
public sealed class WordStudyValidatorsTests
{
    [Fact]
    public async Task NewStudyCommandsShouldValidateResultAnswerAndConcurrencyStamp()
    {
        var memorizationValidator = new SubmitWordMemorizationRequestValidator();
        var spellingValidator = new SubmitWordSpellingRequestValidator();

        (await memorizationValidator.ValidateAsync(
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Remembered,
                Guid.NewGuid()),
            TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
        (await memorizationValidator.ValidateAsync(
            new SubmitWordMemorizationRequest(null, Guid.NewGuid()),
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await memorizationValidator.ValidateAsync(
            new SubmitWordMemorizationRequest(
                WordMemorizationResult.Forgotten,
                Guid.Empty),
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await spellingValidator.ValidateAsync(
            new SubmitWordSpellingRequest
            {
                Answer = "   ",
                ItemConcurrencyStamp = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await spellingValidator.ValidateAsync(
            new SubmitWordSpellingRequest
            {
                Answer = new string('a', 256),
                ItemConcurrencyStamp = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SpellingPromptShouldNotExposeAnswerContent()
    {
        var properties = typeof(WordSpellingPromptResponse)
            .GetProperties()
            .Select(value => value.Name);

        properties.Should().NotContain([
            "Headword",
            "Examples",
            "AudioResourceId"
        ]);
    }
}
