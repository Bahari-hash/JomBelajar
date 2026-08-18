using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

public sealed class UserWordLibraryListRequestValidator
    : AbstractValidator<UserWordLibraryListRequest>
{
    public UserWordLibraryListRequestValidator()
    {
        RuleFor(value => value.Page)
            .GreaterThanOrEqualTo(1)
            .WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(value => value.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrKey(ErrorCodes.PageSizeInvalid);
    }
}
