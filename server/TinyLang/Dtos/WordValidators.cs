using System.Linq.Expressions;
using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 为词条创建和更新请求定义共享的字段、集合和局部唯一性规则。
/// </summary>
/// <typeparam name="T">具体的词条写入请求类型。</typeparam>
internal sealed class WordUpsertRequestValidator<T> : AbstractValidator<T>
    where T : WordUpsertRequest
{
    /// <summary>
    /// 初始化词头、语言、释义和发音目标集合的共享规则。
    /// </summary>
    public WordUpsertRequestValidator()
    {
        RuleFor(value => value.Headword)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.WordHeadwordRequired)
            .MaximumLength(WordConstraints.MaxHeadwordLength)
            .WithErrKey(ErrorCodes.WordHeadwordLengthLimit);
        RuleFor(value => value.Senses)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
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

        RuleFor(value => value.Pronunciations)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .Must(value => value.Count <= WordConstraints.MaxPronunciationCount)
            .WithErrKey(ErrorCodes.WordChildCountLimit)
            .Must(value => value.All(pronunciation => pronunciation is not null))
            .WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .Must(HaveUniquePronunciationIds).WithErrKey(ErrorCodes.WordChildIdConflict)
            .Must(HaveUniquePronunciationSortOrders).WithErrKey(ErrorCodes.WordSortOrderConflict)
            .Must(HaveUniquePronunciationAudioIds)
            .WithErrKey(ErrorCodes.WordPronunciationAudioDuplicate)
            .Must(value => value.Count(item => item.IsDefault) <= 1)
            .WithErrKey(ErrorCodes.WordDefaultPronunciationConflict);
        RuleForEach(value => value.Pronunciations)
            .NotNull().WithErrKey(ErrorCodes.WordChildCollectionInvalid)
            .SetValidator(new WordPronunciationInputValidator());
    }

    /// <summary>
    /// 判断释义已有标识是否互不重复且不是空 GUID。
    /// </summary>
    private static bool HaveUniqueSenseIds(IReadOnlyCollection<WordSenseInput> values)
        => HaveUniqueOptionalIds(values.Select(value => value.Id));

    /// <summary>
    /// 判断全部例句已有标识是否互不重复且不是空 GUID。
    /// </summary>
    private static bool HaveUniqueExampleIds(IReadOnlyCollection<WordSenseInput> values)
        => HaveUniqueOptionalIds(values.SelectMany(value => value.Examples).Select(value => value.Id));

    /// <summary>
    /// 判断发音已有标识是否互不重复且不是空 GUID。
    /// </summary>
    private static bool HaveUniquePronunciationIds(
        IReadOnlyCollection<WordPronunciationInput> values)
        => HaveUniqueOptionalIds(values.Select(value => value.Id));

    /// <summary>
    /// 判断同级释义排序值是否互不重复。
    /// </summary>
    private static bool HaveUniqueSenseSortOrders(IReadOnlyCollection<WordSenseInput> values)
        => values.Select(value => value.SortOrder).Distinct().Count() == values.Count;

    /// <summary>
    /// 判断同级发音排序值是否互不重复。
    /// </summary>
    private static bool HaveUniquePronunciationSortOrders(
        IReadOnlyCollection<WordPronunciationInput> values)
        => values.Select(value => value.SortOrder).Distinct().Count() == values.Count;

    /// <summary>
    /// 判断词条发音音频标识是否互不重复。
    /// </summary>
    private static bool HaveUniquePronunciationAudioIds(
        IReadOnlyCollection<WordPronunciationInput> values)
        => values.Select(value => value.AudioClipId).Distinct().Count() == values.Count;

    /// <summary>
    /// 判断可空标识集合中的非空值合法且互不重复。
    /// </summary>
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
/// 校验词条草稿创建请求，并禁止客户端指定任何子项标识。
/// </summary>
public sealed class CreateWordRequestValidator : AbstractValidator<CreateWordRequest>
{
    /// <summary>
    /// 初始化创建请求和服务端子项标识规则。
    /// </summary>
    public CreateWordRequestValidator()
    {
        Include(new WordUpsertRequestValidator<CreateWordRequest>());
        RuleFor(value => value)
            .Must(value => value.Senses is not null && value.Pronunciations is not null &&
                value.Senses.All(sense => sense is not null && sense.Examples is not null &&
                    sense.Id is null && sense.Examples.All(example =>
                        example is not null && example.Id is null)) &&
                value.Pronunciations.All(pronunciation =>
                    pronunciation is not null && pronunciation.Id is null))
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
    }
}

/// <summary>
/// 校验词条完整更新请求及其客户端并发标识。
/// </summary>
public sealed class UpdateWordRequestValidator : AbstractValidator<UpdateWordRequest>
{
    /// <summary>
    /// 初始化更新请求、并发标识和新释义子项规则。
    /// </summary>
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
/// 校验词条状态动作和硬删除使用的并发标识。
/// </summary>
public sealed class WordMutationRequestValidator : AbstractValidator<WordMutationRequest>
{
    /// <summary>
    /// 要求客户端提供非空并发标识。
    /// </summary>
    public WordMutationRequestValidator()
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
    /// <summary>
    /// 初始化释义、语言、词性、排序和例句规则。
    /// </summary>
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
/// 校验例句纯文本、语言、音频标识和排序。
/// </summary>
public sealed class ExampleSentenceInputValidator
    : AbstractValidator<ExampleSentenceInput>
{
    /// <summary>
    /// 初始化例句正文、翻译、语言、音频和排序规则。
    /// </summary>
    public ExampleSentenceInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
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
        RuleFor(value => value.AudioClipId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordPronunciationAudioInvalid);
        RuleFor(value => value.SortOrder)
            .InclusiveBetween(0, WordConstraints.MaxSortOrder)
            .WithErrKey(ErrorCodes.WordSortOrderInvalid);
    }

}

/// <summary>
/// 校验词条发音的音频、可选元数据和排序。
/// </summary>
public sealed class WordPronunciationInputValidator
    : AbstractValidator<WordPronunciationInput>
{
    /// <summary>
    /// 初始化发音标识、音频、口音、IPA 和排序规则。
    /// </summary>
    public WordPronunciationInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.WordChildIdInvalid);
        RuleFor(value => value.AudioClipId)
            .NotEmpty().WithErrKey(ErrorCodes.WordPronunciationAudioInvalid);
        RuleFor(value => value.AccentTag)
            .MaximumLength(WordConstraints.MaxAccentTagLength)
            .WithErrKey(ErrorCodes.WordAccentTagLengthLimit);
        RuleFor(value => value.Ipa)
            .MaximumLength(WordConstraints.MaxIpaLength)
            .WithErrKey(ErrorCodes.WordIpaLengthLimit);
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
    /// <summary>
    /// 初始化有界分页、关键词、语言和状态筛选规则。
    /// </summary>
    public AdminWordListRequestValidator()
    {
        WordListValidationRules.Add(
            this,
            value => value.Page,
            value => value.PageSize,
            value => value.Keyword);
        RuleFor(value => value.Status)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.WordStatusInvalid);
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
    /// <summary>
    /// 初始化有界分页、关键词和可选语言筛选规则。
    /// </summary>
    public WordListRequestValidator()
    {
        WordListValidationRules.Add(
            this,
            value => value.Page,
            value => value.PageSize,
            value => value.Keyword);
    }
}

/// <summary>
/// 提供两种词条列表请求共享的 FluentValidation 规则。
/// </summary>
internal static class WordListValidationRules
{
    /// <summary>
    /// 为支持分页、关键词和语言的列表请求添加共享规则。
    /// </summary>
    /// <typeparam name="T">具体列表请求类型。</typeparam>
    /// <param name="validator">接收规则的 validator。</param>
    /// <param name="pageExpression">页码字段表达式。</param>
    /// <param name="pageSizeExpression">页大小字段表达式。</param>
    /// <param name="keywordExpression">关键词字段表达式。</param>
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
