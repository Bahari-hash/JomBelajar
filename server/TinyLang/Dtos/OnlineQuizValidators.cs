using System.Linq.Expressions;
using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;
using TinyLang.Services;

namespace TinyLang.Dtos;

/// <summary>
/// 为试卷创建和更新请求定义共享字段、集合和题型规则。
/// </summary>
/// <typeparam name="T">具体的试卷写入请求类型。</typeparam>
internal sealed class PaperUpsertRequestValidator<T> : AbstractValidator<T>
    where T : PaperUpsertRequest
{
    /// <summary>
    /// 初始化试卷文本、语言、评分和完整题目集合规则。
    /// </summary>
    public PaperUpsertRequestValidator()
    {
        RuleFor(value => value.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.PaperTitleRequired)
            .MaximumLength(OnlineQuizConstraints.MaxTitleLength)
            .WithErrKey(ErrorCodes.PaperTitleLengthLimit);
        RuleFor(value => value.Description)
            .MaximumLength(OnlineQuizConstraints.MaxDescriptionLength)
            .WithErrKey(ErrorCodes.PaperDescriptionLengthLimit);
        RuleFor(value => value.Instructions)
            .MaximumLength(OnlineQuizConstraints.MaxInstructionsLength)
            .WithErrKey(ErrorCodes.PaperInstructionsLengthLimit);
        RuleFor(value => value.LanguageTag)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.PaperLanguageInvalid)
            .MaximumLength(OnlineQuizConstraints.MaxLanguageTagLength)
            .WithErrKey(ErrorCodes.PaperLanguageInvalid)
            .Matches(MediaValidationPatterns.LanguageTag())
            .WithErrKey(ErrorCodes.PaperLanguageInvalid);
        RuleFor(value => value.Tags)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.PaperTagInvalid)
            .Must(value => value.Count <= OnlineQuizConstraints.MaxPaperTagCount)
            .WithErrKey(ErrorCodes.PaperTagCountLimit)
            .Must(HaveValidTags)
            .WithErrKey(ErrorCodes.PaperTagInvalid)
            .Must(HaveTagsWithinLengthLimit)
            .WithErrKey(ErrorCodes.PaperTagLengthLimit)
            .Must(HaveUniqueTags)
            .WithErrKey(ErrorCodes.PaperTagDuplicate);
        RuleFor(value => value.PassingScorePercentage)
            .InclusiveBetween(1, 100)
            .WithErrKey(ErrorCodes.PaperPassingScoreInvalid);
        RuleFor(value => value.Questions)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .Must(value => value.Count <= OnlineQuizConstraints.MaxQuestionCount)
            .WithErrKey(ErrorCodes.PaperChildCountLimit)
            .Must(value => value.All(question => question is not null &&
                question.Options is not null && question.AcceptedAnswers is not null))
            .WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .Must(HaveUniqueQuestionIds)
            .WithErrKey(ErrorCodes.PaperChildIdConflict)
            .Must(HaveUniqueQuestionSortOrders)
            .WithErrKey(ErrorCodes.PaperSortOrderConflict)
            .Must(HaveUniqueNestedIds)
            .WithErrKey(ErrorCodes.PaperChildIdConflict);
        RuleForEach(value => value.Questions)
            .NotNull().WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .SetValidator(new PaperQuestionInputValidator());
    }

    /// <summary>
    /// 判断标签均包含可见的非控制字符内容。
    /// </summary>
    private static bool HaveValidTags(IReadOnlyCollection<string> tags)
        => tags.All(tag => !string.IsNullOrWhiteSpace(tag) &&
            !tag.Any(char.IsControl));

    /// <summary>
    /// 判断标签裁剪后的长度均不超过写入边界。
    /// </summary>
    private static bool HaveTagsWithinLengthLimit(
        IReadOnlyCollection<string> tags)
        => tags.All(tag => tag.Trim().Length <=
            OnlineQuizConstraints.MaxPaperTagLength);

    /// <summary>
    /// 判断标签裁剪并小写化后互不重复。
    /// </summary>
    private static bool HaveUniqueTags(IReadOnlyCollection<string> tags)
        => tags.Select(tag => tag.Trim().ToLowerInvariant()).Distinct(
            StringComparer.Ordinal).Count() == tags.Count;

    /// <summary>
    /// 判断已有题目标识合法且互不重复。
    /// </summary>
    private static bool HaveUniqueQuestionIds(
        IReadOnlyCollection<PaperQuestionInput> questions)
        => HaveUniqueOptionalIds(questions.Select(value => value.Id));

    /// <summary>
    /// 判断全部已有选项和标准答案标识在各自集合内互不重复。
    /// </summary>
    private static bool HaveUniqueNestedIds(
        IReadOnlyCollection<PaperQuestionInput> questions)
        => HaveUniqueOptionalIds(questions.SelectMany(value => value.Options)
               .Select(value => value.Id)) &&
            HaveUniqueOptionalIds(questions.SelectMany(value => value.AcceptedAnswers)
               .Select(value => value.Id));

    /// <summary>
    /// 判断题目排序值在试卷内互不重复。
    /// </summary>
    private static bool HaveUniqueQuestionSortOrders(
        IReadOnlyCollection<PaperQuestionInput> questions)
        => questions.Select(value => value.SortOrder).Distinct().Count() ==
            questions.Count;

    /// <summary>
    /// 判断可空标识中的已有值非空且互不重复。
    /// </summary>
    private static bool HaveUniqueOptionalIds(IEnumerable<Guid?> values)
    {
        var ids = values.Where(value => value.HasValue)
            .Select(value => value.GetValueOrDefault()).ToArray();
        return ids.All(value => value != Guid.Empty) &&
            ids.Distinct().Count() == ids.Length;
    }

}

/// <summary>
/// 校验试卷草稿创建请求并禁止客户端指定子项标识。
/// </summary>
public sealed class CreatePaperRequestValidator : AbstractValidator<CreatePaperRequest>
{
    /// <summary>
    /// 初始化创建请求和服务端子项标识规则。
    /// </summary>
    public CreatePaperRequestValidator()
    {
        Include(new PaperUpsertRequestValidator<CreatePaperRequest>());
        RuleFor(value => value)
            .Must(HasNoExistingIds)
            .WithErrKey(ErrorCodes.PaperChildIdInvalid);
    }

    /// <summary>
    /// 判断创建请求没有携带任何服务端子项标识。
    /// </summary>
    private static bool HasNoExistingIds(CreatePaperRequest request)
        => request.Questions is not null && request.Questions.All(question =>
            question is not null && question.Id is null &&
            question.Options is not null &&
            question.Options.All(option => option is not null && option.Id is null) &&
            question.AcceptedAnswers is not null &&
            question.AcceptedAnswers.All(answer => answer is not null && answer.Id is null));
}

/// <summary>
/// 校验试卷完整更新请求、并发标识和新增父子项规则。
/// </summary>
public sealed class UpdatePaperRequestValidator : AbstractValidator<UpdatePaperRequest>
{
    /// <summary>
    /// 初始化更新请求的共享规则和稳定子项边界。
    /// </summary>
    public UpdatePaperRequestValidator()
    {
        Include(new PaperUpsertRequestValidator<UpdatePaperRequest>());
        RuleFor(value => value.ConcurrencyStamp)
            .NotEmpty().WithErrKey(ErrorCodes.PaperConcurrencyConflict);
        RuleFor(value => value)
            .Must(NewQuestionsHaveOnlyNewChildren)
            .WithErrKey(ErrorCodes.PaperChildIdInvalid);
    }

    /// <summary>
    /// 判断新增题目没有携带既有选项或标准答案标识。
    /// </summary>
    private static bool NewQuestionsHaveOnlyNewChildren(UpdatePaperRequest request)
        => request.Questions is not null && request.Questions.All(question =>
            question is not null && (question.Id is not null ||
                (question.Options is not null &&
                 question.Options.All(option => option is not null && option.Id is null) &&
                 question.AcceptedAnswers is not null &&
                 question.AcceptedAnswers.All(answer =>
                     answer is not null && answer.Id is null))));
}

/// <summary>
/// 校验试卷发布检查、状态动作和硬删除使用的并发标识。
/// </summary>
public sealed class PaperMutationRequestValidator
    : AbstractValidator<PaperMutationRequest>
{
    /// <summary>
    /// 要求客户端提供非空试卷并发标识。
    /// </summary>
    public PaperMutationRequestValidator()
    {
        RuleFor(value => value.ConcurrencyStamp)
            .NotEmpty().WithErrKey(ErrorCodes.PaperConcurrencyConflict);
    }
}

/// <summary>
/// 校验题目文本、题型形状、评分、排序和嵌套答案集合。
/// </summary>
public sealed class PaperQuestionInputValidator
    : AbstractValidator<PaperQuestionInput>
{
    /// <summary>
    /// 初始化题目及其选项和标准答案的局部规则。
    /// </summary>
    public PaperQuestionInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.PaperChildIdInvalid);
        RuleFor(value => value.Type)
            .Must(Enum.IsDefined).WithErrKey(ErrorCodes.PaperQuestionTypeInvalid);
        RuleFor(value => value.Prompt)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.PaperQuestionPromptRequired)
            .MaximumLength(OnlineQuizConstraints.MaxPromptLength)
            .WithErrKey(ErrorCodes.PaperQuestionPromptLengthLimit);
        RuleFor(value => value.Explanation)
            .MaximumLength(OnlineQuizConstraints.MaxExplanationLength)
            .WithErrKey(ErrorCodes.PaperExplanationLengthLimit);
        RuleFor(value => value.Points)
            .InclusiveBetween(
                OnlineQuizConstraints.MinPoints,
                OnlineQuizConstraints.MaxPoints)
            .WithErrKey(ErrorCodes.PaperPointsInvalid);
        RuleFor(value => value.SortOrder)
            .InclusiveBetween(0, OnlineQuizConstraints.MaxSortOrder)
            .WithErrKey(ErrorCodes.PaperSortOrderInvalid);

        RuleFor(value => value.Options)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .Must(value => value.Count <= OnlineQuizConstraints.MaxOptionCount)
            .WithErrKey(ErrorCodes.PaperChildCountLimit)
            .Must(value => value.All(option => option is not null))
            .WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .Must(HaveUniqueOptionIds)
            .WithErrKey(ErrorCodes.PaperChildIdConflict)
            .Must(HaveUniqueOptionSortOrders)
            .WithErrKey(ErrorCodes.PaperSortOrderConflict);
        RuleForEach(value => value.Options)
            .NotNull().WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .SetValidator(new PaperQuestionOptionInputValidator());

        RuleFor(value => value.AcceptedAnswers)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .Must(value => value.Count <= OnlineQuizConstraints.MaxAcceptedAnswerCount)
            .WithErrKey(ErrorCodes.PaperChildCountLimit)
            .Must(value => value.All(answer => answer is not null))
            .WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .Must(HaveUniqueAcceptedAnswerIds)
            .WithErrKey(ErrorCodes.PaperChildIdConflict)
            .Must(HaveUniqueAcceptedAnswerSortOrders)
            .WithErrKey(ErrorCodes.PaperSortOrderConflict);
        RuleForEach(value => value.AcceptedAnswers)
            .NotNull().WithErrKey(ErrorCodes.PaperQuestionCollectionInvalid)
            .SetValidator(new FillBlankAcceptedAnswerInputValidator());

        RuleFor(value => value)
            .Must(HaveValidTypeShape)
            .WithErrKey(ErrorCodes.PaperQuestionShapeInvalid);
        RuleFor(value => value)
            .Must(HaveUniqueNormalizedAcceptedAnswers)
            .WithErrKey(ErrorCodes.PaperAcceptedAnswerDuplicate);
    }

    /// <summary>
    /// 判断选项已有标识非空且互不重复。
    /// </summary>
    private static bool HaveUniqueOptionIds(
        IReadOnlyCollection<PaperQuestionOptionInput> values)
        => HaveUniqueOptionalIds(values.Select(value => value.Id));

    /// <summary>
    /// 判断标准答案已有标识非空且互不重复。
    /// </summary>
    private static bool HaveUniqueAcceptedAnswerIds(
        IReadOnlyCollection<FillBlankAcceptedAnswerInput> values)
        => HaveUniqueOptionalIds(values.Select(value => value.Id));

    /// <summary>
    /// 判断选项排序值互不重复。
    /// </summary>
    private static bool HaveUniqueOptionSortOrders(
        IReadOnlyCollection<PaperQuestionOptionInput> values)
        => values.Select(value => value.SortOrder).Distinct().Count() == values.Count;

    /// <summary>
    /// 判断标准答案排序值互不重复。
    /// </summary>
    private static bool HaveUniqueAcceptedAnswerSortOrders(
        IReadOnlyCollection<FillBlankAcceptedAnswerInput> values)
        => values.Select(value => value.SortOrder).Distinct().Count() == values.Count;

    /// <summary>
    /// 判断可空标识中的已有值合法且互不重复。
    /// </summary>
    private static bool HaveUniqueOptionalIds(IEnumerable<Guid?> values)
    {
        var ids = values.Where(value => value.HasValue)
            .Select(value => value.GetValueOrDefault()).ToArray();
        return ids.All(value => value != Guid.Empty) &&
            ids.Distinct().Count() == ids.Length;
    }

    /// <summary>
    /// 判断题目只使用其题型允许的标准答案字段。
    /// </summary>
    private static bool HaveValidTypeShape(PaperQuestionInput value)
    {
        if (value.Options is null || value.AcceptedAnswers is null ||
            value.Options.Any(option => option is null) ||
            value.AcceptedAnswers.Any(answer => answer is null) ||
            !Enum.IsDefined(value.Type))
        {
            return false;
        }

        return value.Type switch
        {
            Entities.Enums.PaperQuestionType.SingleChoice =>
                value.CorrectBoolean is null && !value.FillBlankCaseSensitive &&
                value.AcceptedAnswers.Count == 0 &&
                value.Options.Count(option => option.IsCorrect) <= 1,
            Entities.Enums.PaperQuestionType.TrueFalse =>
                value.Options.Count == 0 && value.AcceptedAnswers.Count == 0 &&
                !value.FillBlankCaseSensitive,
            Entities.Enums.PaperQuestionType.FillBlank =>
                value.Options.Count == 0 && value.CorrectBoolean is null,
            _ => false
        };
    }

    /// <summary>
    /// 判断填空题标准答案在题目大小写策略下规范化后互不重复。
    /// </summary>
    private static bool HaveUniqueNormalizedAcceptedAnswers(PaperQuestionInput value)
    {
        if (value.AcceptedAnswers is null ||
            value.Type != Entities.Enums.PaperQuestionType.FillBlank)
        {
            return true;
        }

        if (value.AcceptedAnswers.Any(answer =>
            answer is null || answer.Text is null))
        {
            return false;
        }

        try
        {
            var normalized = value.AcceptedAnswers.Select(answer =>
                FillBlankAnswerNormalizer.Normalize(
                    answer.Text,
                    value.FillBlankCaseSensitive)).ToArray();
            return normalized.All(answer => answer.Length is > 0 and <=
                    OnlineQuizConstraints.MaxAnswerTextLength) &&
                normalized.Distinct(StringComparer.Ordinal).Count() == normalized.Length;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}

/// <summary>
/// 校验单选题选项文本、标识和排序。
/// </summary>
public sealed class PaperQuestionOptionInputValidator
    : AbstractValidator<PaperQuestionOptionInput>
{
    /// <summary>
    /// 初始化选项字段规则。
    /// </summary>
    public PaperQuestionOptionInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.PaperChildIdInvalid);
        RuleFor(value => value.Text)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrKey(ErrorCodes.PaperOptionTextRequired)
            .MaximumLength(OnlineQuizConstraints.MaxOptionTextLength)
            .WithErrKey(ErrorCodes.PaperOptionTextLengthLimit);
        RuleFor(value => value.SortOrder)
            .InclusiveBetween(0, OnlineQuizConstraints.MaxSortOrder)
            .WithErrKey(ErrorCodes.PaperSortOrderInvalid);
    }
}

/// <summary>
/// 校验填空题标准答案文本、标识和排序。
/// </summary>
public sealed class FillBlankAcceptedAnswerInputValidator
    : AbstractValidator<FillBlankAcceptedAnswerInput>
{
    /// <summary>
    /// 初始化标准答案字段规则。
    /// </summary>
    public FillBlankAcceptedAnswerInputValidator()
    {
        RuleFor(value => value.Id)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.PaperChildIdInvalid);
        RuleFor(value => value.Text)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrKey(ErrorCodes.PaperAcceptedAnswerRequired)
            .MaximumLength(OnlineQuizConstraints.MaxAnswerTextLength)
            .WithErrKey(ErrorCodes.PaperAnswerTextLengthLimit);
        RuleFor(value => value.SortOrder)
            .InclusiveBetween(0, OnlineQuizConstraints.MaxSortOrder)
            .WithErrKey(ErrorCodes.PaperSortOrderInvalid);
    }
}

/// <summary>
/// 校验管理员试卷列表的分页和筛选条件。
/// </summary>
public sealed class AdminPaperListRequestValidator
    : AbstractValidator<AdminPaperListRequest>
{
    /// <summary>
    /// 初始化管理员列表的有界筛选规则。
    /// </summary>
    public AdminPaperListRequestValidator()
    {
        OnlineQuizListValidationRules.Add(
            this,
            value => value.Page,
            value => value.PageSize,
            value => value.Keyword,
            value => value.Language,
            value => value.Tag);
        RuleFor(value => value.Status)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.PaperStatusInvalid);
    }
}

/// <summary>
/// 校验用户试卷目录的分页和筛选条件。
/// </summary>
public sealed class PaperCatalogRequestValidator : AbstractValidator<PaperCatalogRequest>
{
    /// <summary>
    /// 初始化用户目录的有界筛选规则。
    /// </summary>
    public PaperCatalogRequestValidator()
    {
        OnlineQuizListValidationRules.Add(
            this,
            value => value.Page,
            value => value.PageSize,
            value => value.Keyword,
            value => value.Language,
            value => value.Tag);
    }
}

/// <summary>
/// 校验标签目录的分页和名称搜索条件。
/// </summary>
public sealed class PaperTagListRequestValidator
    : AbstractValidator<PaperTagListRequest>
{
    /// <summary>
    /// 初始化标签目录的有界列表规则。
    /// </summary>
    public PaperTagListRequestValidator()
    {
        RuleFor(value => value.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(value => value.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(value => value.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
    }
}
/// <summary>
/// 校验用户测验历史的有界分页参数。
/// </summary>
public sealed class PaperAttemptListRequestValidator
    : AbstractValidator<PaperAttemptListRequest>
{
    /// <summary>
    /// 初始化测验历史分页规则。
    /// </summary>
    public PaperAttemptListRequestValidator()
    {
        RuleFor(value => value.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(value => value.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
    }
}

/// <summary>
/// 校验逐题保存请求恰好包含一个有界答案字段。
/// </summary>
public sealed class SavePaperAttemptAnswerRequestValidator
    : AbstractValidator<SavePaperAttemptAnswerRequest>
{
    /// <summary>
    /// 初始化答案标识、文本长度和互斥字段规则。
    /// </summary>
    public SavePaperAttemptAnswerRequestValidator()
    {
        RuleFor(value => value.SelectedOptionId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.PaperAttemptSelectedOptionInvalid);
        RuleFor(value => value.TextAnswer)
            .MaximumLength(OnlineQuizConstraints.MaxAnswerTextLength)
            .WithErrKey(ErrorCodes.PaperAnswerTextLengthLimit);
        RuleFor(value => value)
            .Must(value => CountAnswers(value) == 1)
            .WithErrKey(ErrorCodes.PaperAttemptAnswerShapeInvalid);
    }

    /// <summary>
    /// 计算请求中具有值的答案字段数量。
    /// </summary>
    private static int CountAnswers(SavePaperAttemptAnswerRequest value)
        => (value.SelectedOptionId.HasValue ? 1 : 0) +
            (value.BooleanAnswer.HasValue ? 1 : 0) +
            (value.TextAnswer is not null ? 1 : 0);
}

/// <summary>
/// 提供试卷管理和用户目录共享的列表验证规则。
/// </summary>
internal static class OnlineQuizListValidationRules
{
    /// <summary>
    /// 为具有分页、关键词和语言字段的请求添加共享规则。
    /// </summary>
    public static void Add<T>(
        AbstractValidator<T> validator,
        Expression<Func<T, int>> pageExpression,
        Expression<Func<T, int>> pageSizeExpression,
        Expression<Func<T, string?>> keywordExpression,
        Expression<Func<T, string?>> languageExpression,
        Expression<Func<T, string?>> tagExpression)
        where T : class
    {
        var getLanguage = languageExpression.Compile();
        validator.RuleFor(pageExpression)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        validator.RuleFor(pageSizeExpression)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        validator.RuleFor(keywordExpression)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
        validator.RuleFor(languageExpression)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(OnlineQuizConstraints.MaxLanguageTagLength)
            .WithErrKey(ErrorCodes.PaperLanguageInvalid)
            .Must(value => value is null || !string.IsNullOrWhiteSpace(value))
            .WithErrKey(ErrorCodes.PaperLanguageInvalid);
        validator.RuleFor(languageExpression)
            .Matches(MediaValidationPatterns.LanguageTag())
            .When(value => !string.IsNullOrWhiteSpace(getLanguage(value)))
            .WithErrKey(ErrorCodes.PaperLanguageInvalid);
        validator.RuleFor(tagExpression)
            .MaximumLength(OnlineQuizConstraints.MaxPaperTagLength)
            .WithErrKey(ErrorCodes.PaperTagLengthLimit)
            .Must(value => value is null || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.PaperTagInvalid);
    }
}
