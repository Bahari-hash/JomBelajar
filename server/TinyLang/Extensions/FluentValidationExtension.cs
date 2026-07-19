using FluentValidation;
using TinyLang.Exceptions;

namespace TinyLang.Extensions;

public static class FluentValidationExtension
{
    public static IRuleBuilderOptions<T, TProperty> WithErrKey<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder,
        ErrorCodes errorCode)
    {
        return ruleBuilder
            .WithErrorCode(errorCode.ToString())
            .WithMessage(errorCode.GetMessage());
    }
}
