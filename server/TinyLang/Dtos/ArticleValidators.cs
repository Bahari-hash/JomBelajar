using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 为文章创建和更新请求定义共享校验规则。
/// </summary>
/// <typeparam name="T">具体的文章写入请求类型。</typeparam>
internal sealed class ArticleUpsertRequestValidator<T> : AbstractValidator<T>
    where T : ArticleUpsertRequest
{
    /// <summary>
    /// 初始化文章标题、正文、分类和媒体关联的共享校验规则。
    /// </summary>
    public ArticleUpsertRequestValidator()
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.ArticleTitleLengthLimit);

        RuleFor(x => x.Summary)
            .MaximumLength(500).WithErrKey(ErrorCodes.ArticleSummaryLengthLimit);

        RuleFor(x => x.ContentMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleContentRequired)
            .MaximumLength(ArticleConstraints.MaxContentLength)
            .WithErrKey(ErrorCodes.ArticleContentLengthLimit);

        RuleFor(x => x.CategoryIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.ArticleCategoryInvalid)
            .Must(ids => ids.Count <= ArticleConstraints.MaxCategoryCount)
            .WithErrKey(ErrorCodes.ArticleCategoryCountLimit)
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithErrKey(ErrorCodes.ArticleCategoryInvalid)
            .Must(ids => ids.Count == ids.Distinct().Count())
            .WithErrKey(ErrorCodes.ArticleCategoryDuplicate);

        RuleFor(x => x.CoverMediaResourceId)
            .Must(BeNullOrNonEmptyGuid).WithErrKey(ErrorCodes.ArticleMediaInvalid);

        RuleFor(x => x.BodyMediaResourceIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.ArticleMediaInvalid)
            .Must(ids => ids.Count <= ArticleConstraints.MaxBodyMediaCount)
            .WithErrKey(ErrorCodes.ArticleMediaCountLimit)
            .Must(ids => ids.All(id => id != Guid.Empty)).WithErrKey(ErrorCodes.ArticleMediaInvalid)
            .Must(ids => ids.Count == ids.Distinct().Count()).WithErrKey(ErrorCodes.ArticleMediaDuplicate);
    }

    /// <summary>
    /// 判断可空标识是否为空或包含非空 GUID。
    /// </summary>
    /// <param name="value">待校验的可空标识。</param>
    /// <returns>值为空或不是 <see cref="Guid.Empty"/> 时返回 <see langword="true"/>。</returns>
    private static bool BeNullOrNonEmptyGuid(Guid? value)
        => value is null || value != Guid.Empty;

}

/// <summary>
/// 校验文章草稿创建请求。
/// </summary>
public sealed class CreateArticleRequestValidator : AbstractValidator<CreateArticleRequest>
{
    /// <summary>
    /// 初始化文章草稿创建请求的共享规则。
    /// </summary>
    public CreateArticleRequestValidator()
    {
        Include(new ArticleUpsertRequestValidator<CreateArticleRequest>());
    }
}

/// <summary>
/// 校验文章更新请求。
/// </summary>
public sealed class UpdateArticleRequestValidator : AbstractValidator<UpdateArticleRequest>
{
    /// <summary>
    /// 初始化文章更新请求的共享规则。
    /// </summary>
    public UpdateArticleRequestValidator()
    {
        Include(new ArticleUpsertRequestValidator<UpdateArticleRequest>());
        RuleFor(x => x.ConcurrencyStamp)
            .NotEqual(Guid.Empty).WithErrKey(ErrorCodes.ArticleConcurrencyConflict);
    }
}

/// <summary>
/// Validates Markdown submitted to the canonical article preview endpoint.
/// </summary>
public sealed class ArticlePreviewRequestValidator : AbstractValidator<ArticlePreviewRequest>
{
    /// <summary>
    /// Initializes the required Markdown and length rules for previews.
    /// </summary>
    public ArticlePreviewRequestValidator()
    {
        RuleFor(x => x.ContentMarkdown)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleContentRequired)
            .MaximumLength(ArticleConstraints.MaxContentLength)
            .WithErrKey(ErrorCodes.ArticleContentLengthLimit);
    }
}

/// <summary>
/// 校验文章分类创建请求。
/// </summary>
public sealed class CreateArticleCategoryRequestValidator : AbstractValidator<CreateArticleCategoryRequest>
{
    /// <summary>
    /// 初始化文章分类创建请求的校验规则。
    /// </summary>
    public CreateArticleCategoryRequestValidator()
    {
        AddCategoryRules();
    }

    /// <summary>
    /// 添加分类名称、slug 和描述的约束。
    /// </summary>
    private void AddCategoryRules()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleCategoryNameRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.ArticleCategoryNameLengthLimit);

        RuleFor(x => x.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleCategorySlugRequired)
            .MaximumLength(120).WithErrKey(ErrorCodes.ArticleCategorySlugLengthLimit)
            .Matches("^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$")
            .WithErrKey(ErrorCodes.ArticleCategorySlugFormatInvalid);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithErrKey(ErrorCodes.ArticleCategoryDescriptionLengthLimit);
    }
}

/// <summary>
/// 校验文章分类更新请求。
/// </summary>
public sealed class UpdateArticleCategoryRequestValidator : AbstractValidator<UpdateArticleCategoryRequest>
{
    /// <summary>
    /// 初始化文章分类更新请求的校验规则。
    /// </summary>
    public UpdateArticleCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleCategoryNameRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.ArticleCategoryNameLengthLimit);

        RuleFor(x => x.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleCategorySlugRequired)
            .MaximumLength(120).WithErrKey(ErrorCodes.ArticleCategorySlugLengthLimit)
            .Matches("^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$")
            .WithErrKey(ErrorCodes.ArticleCategorySlugFormatInvalid);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithErrKey(ErrorCodes.ArticleCategoryDescriptionLengthLimit);
    }
}

/// <summary>
/// 校验文章列表的分页和筛选条件。
/// </summary>
public sealed class ArticleListRequestValidator : AbstractValidator<ArticleListRequest>
{
    /// <summary>
    /// 初始化文章列表请求的校验规则。
    /// </summary>
    public ArticleListRequestValidator()
    {
        AddPaginationRules();

        RuleFor(x => x.CategoryId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.ArticleCategoryInvalid);

        RuleFor(x => x.Status)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.ArticleStatusInvalid);
    }

    /// <summary>
    /// 添加页码、页大小和关键词的通用约束。
    /// </summary>
    private void AddPaginationRules()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(BeSafeKeyword).WithErrKey(ErrorCodes.KeywordInvalid);
    }

    /// <summary>
    /// 判断关键词是否为空或不包含控制字符。
    /// </summary>
    /// <param name="value">待校验的关键词。</param>
    /// <returns>关键词可以安全参与筛选时返回 <see langword="true"/>。</returns>
    private static bool BeSafeKeyword(string? value)
        => string.IsNullOrEmpty(value) || !value.Any(char.IsControl);
}

/// <summary>
/// 校验文章分类列表的分页和关键词条件。
/// </summary>
public sealed class ArticleCategoryListRequestValidator : AbstractValidator<ArticleCategoryListRequest>
{
    /// <summary>
    /// 初始化文章分类列表请求的校验规则。
    /// </summary>
    public ArticleCategoryListRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
    }
}
