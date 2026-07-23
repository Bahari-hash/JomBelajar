using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

internal sealed class ArticleUpsertRequestValidator<T> : AbstractValidator<T>
    where T : ArticleUpsertRequest
{
    public ArticleUpsertRequestValidator()
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.ArticleTitleLengthLimit);

        RuleFor(x => x.Summary)
            .MaximumLength(500).WithErrKey(ErrorCodes.ArticleSummaryLengthLimit);

        RuleFor(x => x.ContentHtml)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.ArticleContentRequired)
            .MaximumLength(1_000_000).WithErrKey(ErrorCodes.ArticleContentLengthLimit);

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

        RuleFor(x => x.MediaResourceIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.ArticleMediaInvalid)
            .Must(ids => ids.Count <= 100).WithErrKey(ErrorCodes.ArticleMediaCountLimit)
            .Must(ids => ids.All(id => id != Guid.Empty)).WithErrKey(ErrorCodes.ArticleMediaInvalid)
            .Must(ids => ids.Count == ids.Distinct().Count()).WithErrKey(ErrorCodes.ArticleMediaDuplicate);
    }

    private static bool BeNullOrNonEmptyGuid(Guid? value)
        => value is null || value != Guid.Empty;

}

public sealed class CreateArticleRequestValidator : AbstractValidator<CreateArticleRequest>
{
    public CreateArticleRequestValidator()
    {
        Include(new ArticleUpsertRequestValidator<CreateArticleRequest>());
    }
}

public sealed class UpdateArticleRequestValidator : AbstractValidator<UpdateArticleRequest>
{
    public UpdateArticleRequestValidator()
    {
        Include(new ArticleUpsertRequestValidator<UpdateArticleRequest>());
    }
}

public sealed class CreateArticleCategoryRequestValidator : AbstractValidator<CreateArticleCategoryRequest>
{
    public CreateArticleCategoryRequestValidator()
    {
        AddCategoryRules();
    }

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

public sealed class UpdateArticleCategoryRequestValidator : AbstractValidator<UpdateArticleCategoryRequest>
{
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

public sealed class ArticleListRequestValidator : AbstractValidator<ArticleListRequest>
{
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

    private static bool BeSafeKeyword(string? value)
        => string.IsNullOrEmpty(value) || !value.Any(char.IsControl);
}

public sealed class ArticleCategoryListRequestValidator : AbstractValidator<ArticleCategoryListRequest>
{
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
