using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 校验背诵会话的数量、抽词模式和可选语言范围。
/// </summary>
public sealed class CreateWordStudySessionRequestValidator
    : AbstractValidator<CreateWordStudySessionRequest>
{
    /// <summary>
    /// 初始化创建会话请求的有界选择规则。
    /// </summary>
    public CreateWordStudySessionRequestValidator()
    {
        RuleFor(value => value.WordCount)
            .InclusiveBetween(
                WordStudyConstraints.MinWordCount,
                WordStudyConstraints.MaxWordCount)
            .WithErrKey(ErrorCodes.WordStudyWordCountInvalid);
        RuleFor(value => value.SelectionMode)
            .Must(Enum.IsDefined)
            .WithErrKey(ErrorCodes.WordStudySelectionModeInvalid);
        RuleFor(value => value.LanguageTag)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(WordConstraints.MaxLanguageTagLength)
            .WithErrKey(ErrorCodes.WordLanguageInvalid)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithErrKey(ErrorCodes.WordLanguageInvalid);
        RuleFor(value => value.LanguageTag)
            .Matches(MediaValidationPatterns.LanguageTag())
            .When(value => !string.IsNullOrWhiteSpace(value.LanguageTag))
            .WithErrKey(ErrorCodes.WordLanguageInvalid);
    }
}

/// <summary>
/// 校验会话项只能提交基础背诵模块支持的二元结果。
/// </summary>
public sealed class SubmitWordStudyResultRequestValidator
    : AbstractValidator<SubmitWordStudyResultRequest>
{
    /// <summary>
    /// 初始化 Remembered 和 Forgotten 结果枚举规则。
    /// </summary>
    public SubmitWordStudyResultRequestValidator()
    {
        RuleFor(value => value.Result)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .WithErrKey(ErrorCodes.WordStudyResultInvalid)
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.WordStudyResultInvalid);
    }
}
