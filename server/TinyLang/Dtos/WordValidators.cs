using System.Linq.Expressions;
using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 为词条创建和更新请求定义共享的字段、集合和局部唯一性规则。
/// </summary>
internal sealed class WordUpsertRequestValidator<T> : AbstractValidator<T>
    where T : WordUpsertRequest
{
    public WordUpsertRequestValidator()
    {
        RuleFor(value => value.Headword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.WordHeadwordRequired)
            .MaximumLength(WordConstraints.MaxHeadwordLength)
            .WithErrKey(ErrorCodes.WordHeadwordLengthLimit);
        RuleFor(value => value.AudioResourceId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordAudioInvalid);
        RuleFor(value => value.Senses)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .Must(value => value.Count > 0)
            .WithErrKey(ErrorCodes.WordSenseRequired)
            .Must(value => value.Count <= WordConstraints.MaxSenseCount)
            .WithErrKey(ErrorCodes.WordChildCountLimit)
            .Must(value => value.All(sense => sense is not null && sense.Examples is not null))
            .WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .Must(HaveUniqueSenseIds).WithErrKey(ErrorCodes.WordChildIdConflict)
            .Must(HaveUniqueSenseSortOrders).WithErrKey(ErrorCodes.WordSortOrderConflict)
            .Must(HaveUniqueExampleIds).WithErrKey(ErrorCodes.WordChildIdConflict);
        RuleForEach(value => value.Senses)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .SetValidator(new WordSenseInputValidator());
    }

    private static bool HaveUniqueSenseIds(IReadOnlyCollection<WordSenseInput> values)
        => HaveUniqueOptionalIds(values.Select(value => value.Id));

    private static bool HaveUniqueExampleIds(IReadOnlyCollection<WordSenseInput> values)
        => HaveUniqueOptionalIds(values.SelectMany(value => value.Examples)
            .Select(value => value.Id));

    private static bool HaveUniqueSenseSortOrders(IReadOnlyCollection<WordSenseInput> values)
        => values.Select(value => value.SortOrder).Distinct().Count() == values.Count;

    private static bool HaveUniqueOptionalIds(IEnumerable<Guid?> values)
    {
        var existingIds = values.Where(value => value.HasValue)
            .Select(value => value.GetValueOrDefault())
            .ToArray();
        return existingIds.All(value => value != Guid.Empty) &&
            existingIds.Distinct().Count() == existingIds.Length;
    }
}

/// <summary>
/// 校验词条创建请求，并禁止客户端指定任何子项标识。
/// </summary>
public sealed class CreateWordRequestValidator : AbstractValidator<CreateWordRequest>
{
    public CreateWordRequestValidator()
    {
        Include(new WordUpsertRequestValidator<CreateWordRequest>());
        RuleFor(value => value)
            .Must(value => value.Senses is not null &&
                value.Senses.All(sense => sense is not null &&
                    sense.Examples is not null && sense.Id is null &&
                    sense.Examples.All(example =>
                        example is not null && example.Id is null)))
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
    }
}

/// <summary>
/// 校验词条完整更新请求及其客户端并发标识。
/// </summary>
public sealed class UpdateWordRequestValidator : AbstractValidator<UpdateWordRequest>
{
    public UpdateWordRequestValidator()
    {
        Include(new WordUpsertRequestValidator<UpdateWordRequest>());
        RuleFor(value => value.ConcurrencyStamp)
            .NotEmpty().WithErrKey(ErrorCodes.WordConcurrencyConflict);
        RuleFor(value => value.Senses)
            .Must(values => values is not null && values.All(sense =>
                sense is not null &&
                (sense.Id is not null ||
                 (sense.Examples is not null && sense.Examples.All(example =>
                     example is not null && example.Id is null)))))
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
    }
}

/// <summary>
/// 校验硬删除使用的并发标识。
/// </summary>
public sealed class DeleteWordRequestValidator : AbstractValidator<DeleteWordRequest>
{
    public DeleteWordRequestValidator()
    {
        RuleFor(value => value.ConcurrencyStamp)
            .NotEmpty().WithErrKey(ErrorCodes.WordConcurrencyConflict);
    }
}

/// <summary>
/// 校验释义字段、排序和完整例句集合。
/// </summary>
public sealed class WordSenseInputValidator : AbstractValidator<WordSenseInput>
{
    public WordSenseInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
        RuleFor(value => value.PartOfSpeech)
            .Must(Enum.IsDefined).WithErrKey(ErrorCodes.WordPartOfSpeechInvalid);
        RuleFor(value => value.Definition)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.WordDefinitionRequired)
            .MaximumLength(WordConstraints.MaxTextLength)
            .WithErrKey(ErrorCodes.WordDefinitionLengthLimit);
        RuleFor(value => value.UsageNote)
            .MaximumLength(WordConstraints.MaxUsageNoteLength)
            .WithErrKey(ErrorCodes.WordUsageNoteLengthLimit);
        RuleFor(value => value.SortOrder)
            .InclusiveBetween(0, WordConstraints.MaxSortOrder)
            .WithErrKey(ErrorCodes.WordSortOrderInvalid);
        RuleFor(value => value.Examples)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .Must(value => value.Count <= WordConstraints.MaxExampleCount)
            .WithErrKey(ErrorCodes.WordChildCountLimit)
            .Must(value => value.All(example => example is not null))
            .WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .Must(value => value.Select(example => example.SortOrder).Distinct().Count() == value.Count)
            .WithErrKey(ErrorCodes.WordSortOrderConflict);
        RuleForEach(value => value.Examples)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .SetValidator(new ExampleSentenceInputValidator());
    }
}

/// <summary>
/// 校验例句纯文本和排序。
/// </summary>
public sealed class ExampleSentenceInputValidator
    : AbstractValidator<ExampleSentenceInput>
{
    public ExampleSentenceInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
        RuleFor(value => value.AudioResourceId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordExampleAudioInvalid);
        RuleFor(value => value.Sentence)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.WordSentenceRequired)
            .MaximumLength(WordConstraints.MaxTextLength)
            .WithErrKey(ErrorCodes.WordSentenceLengthLimit);
        RuleFor(value => value.Translation)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.WordTranslationRequired)
            .MaximumLength(WordConstraints.MaxTextLength)
            .WithErrKey(ErrorCodes.WordTranslationLengthLimit);
        RuleFor(value => value.SortOrder)
            .InclusiveBetween(0, WordConstraints.MaxSortOrder)
            .WithErrKey(ErrorCodes.WordSortOrderInvalid);
    }
}

/// <summary>
/// 校验管理员词条列表的分页和筛选条件。
/// </summary>
public sealed class AdminWordListRequestValidator
    : AbstractValidator<AdminWordListRequest>
{
    public AdminWordListRequestValidator()
    {
        WordListValidationRules.Add(
            this,
            value => value.Page,
            value => value.PageSize,
            value => value.Keyword);
        RuleFor(value => value.PartOfSpeech)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.WordPartOfSpeechInvalid);
        RuleFor(value => value.Definition)
            .MaximumLength(WordConstraints.MaxTextLength)
            .WithErrKey(ErrorCodes.WordDefinitionLengthLimit)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithErrKey(ErrorCodes.WordDefinitionRequired)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
    }
}

/// <summary>
/// 校验登录用户词条列表的分页和筛选条件。
/// </summary>
public sealed class WordListRequestValidator : AbstractValidator<WordListRequest>
{
    public WordListRequestValidator()
    {
        WordListValidationRules.Add(
            this,
            value => value.Page,
            value => value.PageSize,
            value => value.Keyword);
    }
}

internal static class WordListValidationRules
{
    public static void Add<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, int>> pageExpression,
        Expression<Func<T, int>> pageSizeExpression,
        Expression<Func<T, string?>> keywordExpression)
        where T : class
    {
        validator.RuleFor(pageExpression)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        validator.RuleFor(pageSizeExpression)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        validator.RuleFor(keywordExpression)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
    }
}
