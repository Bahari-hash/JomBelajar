using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证基础单词背诵请求的数量和枚举边界。
/// </summary>
public sealed class WordStudyValidatorsTests
{
    /// <summary>
    /// 验证默认创建请求和两个合法结果均通过校验。
    /// </summary>
    [Fact]
    public async Task ValidRequestsShouldPassValidation()
    {
        var createResult = await new CreateWordStudySessionRequestValidator()
            .ValidateAsync(
                new CreateWordStudySessionRequest(),
                TestContext.Current.CancellationToken);
        var rememberedResult = await new SubmitWordStudyResultRequestValidator()
            .ValidateAsync(
                new SubmitWordStudyResultRequest
                {
                    Result = WordStudyResult.Remembered
                },
                TestContext.Current.CancellationToken);
        var forgottenResult = await new SubmitWordStudyResultRequestValidator()
            .ValidateAsync(
                new SubmitWordStudyResultRequest
                {
                    Result = WordStudyResult.Forgotten
                },
                TestContext.Current.CancellationToken);

        createResult.IsValid.Should().BeTrue();
        rememberedResult.IsValid.Should().BeTrue();
        forgottenResult.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证数量上下界之外和未定义模式均被拒绝。
    /// </summary>
    [Fact]
    public async Task CreateShouldRejectInvalidCountAndMode()
    {
        var validator = new CreateWordStudySessionRequestValidator();
        var tooSmall = await validator.ValidateAsync(
            new CreateWordStudySessionRequest { WordCount = 0 },
            TestContext.Current.CancellationToken);
        var tooLarge = await validator.ValidateAsync(
            new CreateWordStudySessionRequest { WordCount = 101 },
            TestContext.Current.CancellationToken);
        var invalidMode = await validator.ValidateAsync(
            new CreateWordStudySessionRequest
            {
                SelectionMode = (WordStudySelectionMode)999
            },
            TestContext.Current.CancellationToken);

        tooSmall.IsValid.Should().BeFalse();
        tooLarge.IsValid.Should().BeFalse();
        invalidMode.IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证未定义的背诵结果枚举不能进入业务服务。
    /// </summary>
    [Fact]
    public async Task ResultShouldRejectUndefinedEnum()
    {
        var validator = new SubmitWordStudyResultRequestValidator();
        var missing = await validator.ValidateAsync(
            new SubmitWordStudyResultRequest(),
            TestContext.Current.CancellationToken);
        var result = await validator
            .ValidateAsync(
                new SubmitWordStudyResultRequest
                {
                    Result = (WordStudyResult)999
                },
                TestContext.Current.CancellationToken);

        missing.IsValid.Should().BeFalse();
        result.IsValid.Should().BeFalse();
    }
}
