using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

public sealed class SubmitWordMemorizationRequestValidator
    : AbstractValidator<SubmitWordMemorizationRequest>
{
    public SubmitWordMemorizationRequestValidator()
    {
        RuleFor(value => value.Result)
            .NotNull()
            .Must(value => value.HasValue && Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.WordStudyMemorizationResultInvalid);
        RuleFor(value => value.ItemConcurrencyStamp)
            .NotEqual(Guid.Empty)
            .WithErrKey(ErrorCodes.WordStudyConcurrencyConflict);
    }
}

public sealed class SubmitWordSpellingRequestValidator
    : AbstractValidator<SubmitWordSpellingRequest>
{
    public SubmitWordSpellingRequestValidator()
    {
        RuleFor(value => value.Answer)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.WordStudySpellingRequired)
            .When(value => !value.Skip, ApplyConditionTo.CurrentValidator)
            .Must(value => value is not null && value.Trim().Length <= 255)
            .WithErrKey(ErrorCodes.WordStudySpellingLengthLimit);
        RuleFor(value => value.ItemConcurrencyStamp)
            .NotEqual(Guid.Empty)
            .WithErrKey(ErrorCodes.WordStudyConcurrencyConflict);
    }
}

public sealed class ExcludeWordFromReviewRequestValidator
    : AbstractValidator<ExcludeWordFromReviewRequest>
{
    public ExcludeWordFromReviewRequestValidator()
        => RuleFor(value => value.ItemConcurrencyStamp)
            .NotEqual(Guid.Empty)
            .WithErrKey(ErrorCodes.WordStudyConcurrencyConflict);
}
