using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

public sealed record WordStudyTodayReviewRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed class WordStudyTodayReviewRequestValidator
    : AbstractValidator<WordStudyTodayReviewRequest>
{
    public WordStudyTodayReviewRequestValidator()
    {
        RuleFor(value => value.Page)
            .GreaterThanOrEqualTo(1)
            .WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(value => value.PageSize)
            .InclusiveBetween(1, 100)
            .WithErrKey(ErrorCodes.PageSizeInvalid);
    }
}

public sealed record WordStudyCheckInCalendarRequest
{
    public int? Year { get; init; }
    public int? Month { get; init; }
}

public sealed class WordStudyCheckInCalendarRequestValidator
    : AbstractValidator<WordStudyCheckInCalendarRequest>
{
    public WordStudyCheckInCalendarRequestValidator()
    {
        RuleFor(value => value.Year)
            .Must(value => value is null or >= 1 and <= 9999)
            .WithErrKey(ErrorCodes.WordStudyYearInvalid);
        RuleFor(value => value.Month)
            .Must(value => value is null or >= 1 and <= 12)
            .WithErrKey(ErrorCodes.WordStudyMonthInvalid);
    }
}
