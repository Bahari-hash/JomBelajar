using System.Text.RegularExpressions;
using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 校验创建视频请求中的源资源和展示元数据。
/// </summary>
public sealed class CreateVideoRequestValidator : AbstractValidator<CreateVideoRequest>
{
    /// <summary>
    /// 创建视频源标识、标题、简介和语言标签规则。
    /// </summary>
    public CreateVideoRequestValidator()
    {
        RuleFor(x => x.SourceMediaResourceId)
            .NotEmpty().WithErrKey(ErrorCodes.VideoSourceInvalid);
        AddMetadataRules(this);
    }

    /// <summary>
    /// 添加创建视频时共享的标题、简介和语言标签规则。
    /// </summary>
    private static void AddMetadataRules(AbstractValidator<CreateVideoRequest> validator)
    {
        validator.RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.VideoTitleLengthLimit);
        validator.RuleFor(x => x.Description)
            .MaximumLength(2000).WithErrKey(ErrorCodes.VideoDescriptionLengthLimit);
        validator.RuleFor(x => x.OriginalLanguage)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoLanguageInvalid)
            .MaximumLength(35).WithErrKey(ErrorCodes.VideoLanguageInvalid)
            .Matches(VideoValidationPatterns.LanguageTag())
            .WithErrKey(ErrorCodes.VideoLanguageInvalid);
        validator.RuleFor(x => x.CategoryIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.VideoCategoryIdsInvalid)
            .Must(ids => ids.Count <= VideoConstraints.MaxCategoryCount)
            .WithErrKey(ErrorCodes.VideoCategoryCountLimit)
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithErrKey(ErrorCodes.VideoCategoryIdsInvalid)
            .Must(ids => ids.Count == ids.Distinct().Count())
            .WithErrKey(ErrorCodes.VideoCategoryDuplicate);
    }
}

/// <summary>
/// 校验视频元数据更新请求。
/// </summary>
public sealed class UpdateVideoRequestValidator : AbstractValidator<UpdateVideoRequest>
{
    /// <summary>
    /// 创建标题、简介和 BCP 47 风格语言标签规则。
    /// </summary>
    public UpdateVideoRequestValidator()
    {
        RuleFor(x => x.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.VideoTitleLengthLimit);
        RuleFor(x => x.Description)
            .MaximumLength(2000).WithErrKey(ErrorCodes.VideoDescriptionLengthLimit);
        RuleFor(x => x.OriginalLanguage)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoLanguageInvalid)
            .MaximumLength(35).WithErrKey(ErrorCodes.VideoLanguageInvalid)
            .Matches(VideoValidationPatterns.LanguageTag())
            .WithErrKey(ErrorCodes.VideoLanguageInvalid);
        RuleFor(x => x.CategoryIds)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithErrKey(ErrorCodes.VideoCategoryIdsInvalid)
            .Must(ids => ids.Count <= VideoConstraints.MaxCategoryCount)
            .WithErrKey(ErrorCodes.VideoCategoryCountLimit)
            .Must(ids => ids.All(id => id != Guid.Empty))
            .WithErrKey(ErrorCodes.VideoCategoryIdsInvalid)
            .Must(ids => ids.Count == ids.Distinct().Count())
            .WithErrKey(ErrorCodes.VideoCategoryDuplicate);
    }
}

/// <summary>
/// 校验编辑者视频列表分页和关键词边界。
/// </summary>
public sealed class EditorVideoListRequestValidator
    : AbstractValidator<EditorVideoListRequest>
{
    /// <summary>
    /// 创建通用分页和有界关键词规则。
    /// </summary>
    public EditorVideoListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword).MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit);
        RuleFor(x => x.CategoryId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.VideoCategoryIdsInvalid);
    }
}

/// <summary>
/// 校验登录用户视频目录分页和关键词边界。
/// </summary>
public sealed class VideoCatalogRequestValidator : AbstractValidator<VideoCatalogRequest>
{
    /// <summary>
    /// 创建通用分页和有界关键词规则。
    /// </summary>
    public VideoCatalogRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword).MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit);
        RuleFor(x => x.CategoryId)
            .Must(value => value is null || value != Guid.Empty)
            .WithErrKey(ErrorCodes.VideoCategoryIdsInvalid);
    }
}

/// <summary>
/// 校验 WebVTT 字幕关联的资源、语言、显示名称和排序。
/// </summary>
public sealed class AddVideoSubtitleRequestValidator
    : AbstractValidator<AddVideoSubtitleRequest>
{
    /// <summary>
    /// 创建字幕资源和 BCP 47 风格语言标签规则。
    /// </summary>
    public AddVideoSubtitleRequestValidator()
    {
        RuleFor(x => x.MediaResourceId)
            .NotEmpty().WithErrKey(ErrorCodes.VideoSubtitleInvalid);
        RuleFor(x => x.LanguageTag)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoLanguageInvalid)
            .MaximumLength(35).WithErrKey(ErrorCodes.VideoLanguageInvalid)
            .Matches(VideoValidationPatterns.LanguageTag())
            .WithErrKey(ErrorCodes.VideoLanguageInvalid);
        RuleFor(x => x.DisplayName)
            .NotEmpty().WithErrKey(ErrorCodes.VideoSubtitleInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.VideoSubtitleInvalid);
        RuleFor(x => x.SortOrder)
            .InclusiveBetween(0, 1000).WithErrKey(ErrorCodes.VideoSubtitleInvalid);
    }
}

/// <summary>
/// 校验客户端播放位置为有限非负数。
/// </summary>
public sealed class UpdateVideoProgressRequestValidator
    : AbstractValidator<UpdateVideoProgressRequest>
{
    /// <summary>
    /// 创建有限非负播放位置规则。
    /// </summary>
    public UpdateVideoProgressRequestValidator()
    {
        RuleFor(x => x.PositionSeconds)
            .Must(value => double.IsFinite(value) && value >= 0)
            .WithErrKey(ErrorCodes.VideoProgressInvalid);
    }
}

/// <summary>
/// 提供视频 DTO 共享的源生成正则表达式。
/// </summary>
internal static partial class VideoValidationPatterns
{
    /// <summary>
    /// 匹配适用于本模块基础验证的 BCP 47 风格语言标签。
    /// </summary>
    [GeneratedRegex("^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    public static partial Regex LanguageTag();
}

/// <summary>
/// 校验视频分类创建请求的名称、slug 和描述边界。
/// </summary>
public sealed class CreateVideoCategoryRequestValidator
    : AbstractValidator<CreateVideoCategoryRequest>
{
    /// <summary>
    /// 初始化视频分类创建请求校验规则。
    /// </summary>
    public CreateVideoCategoryRequestValidator()
    {
        AddCategoryRules();
    }

    /// <summary>
    /// 添加分类名称、slug 和描述规则。
    /// </summary>
    private void AddCategoryRules()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoCategoryNameRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.VideoCategoryNameLengthLimit);
        RuleFor(x => x.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoCategorySlugRequired)
            .MaximumLength(120).WithErrKey(ErrorCodes.VideoCategorySlugLengthLimit)
            .Matches("^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$")
            .WithErrKey(ErrorCodes.VideoCategorySlugFormatInvalid);
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithErrKey(ErrorCodes.VideoCategoryDescriptionLengthLimit);
    }
}

/// <summary>
/// 校验视频分类更新请求的名称、slug 和描述边界。
/// </summary>
public sealed class UpdateVideoCategoryRequestValidator
    : AbstractValidator<UpdateVideoCategoryRequest>
{
    /// <summary>
    /// 初始化视频分类更新请求校验规则。
    /// </summary>
    public UpdateVideoCategoryRequestValidator()
    {
        RuleFor(x => x.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoCategoryNameRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.VideoCategoryNameLengthLimit);
        RuleFor(x => x.Slug)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.VideoCategorySlugRequired)
            .MaximumLength(120).WithErrKey(ErrorCodes.VideoCategorySlugLengthLimit)
            .Matches("^[A-Za-z0-9]+(?:-[A-Za-z0-9]+)*$")
            .WithErrKey(ErrorCodes.VideoCategorySlugFormatInvalid);
        RuleFor(x => x.Description)
            .MaximumLength(500)
            .WithErrKey(ErrorCodes.VideoCategoryDescriptionLengthLimit);
    }
}

/// <summary>
/// 校验视频分类用户列表分页和关键词条件。
/// </summary>
public sealed class VideoCategoryListRequestValidator
    : AbstractValidator<VideoCategoryListRequest>
{
    /// <summary>
    /// 初始化视频分类列表分页和关键词规则。
    /// </summary>
    public VideoCategoryListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
    }
}

/// <summary>
/// 校验管理员视频分类列表的分页和关键词条件。
/// </summary>
public sealed class AdminVideoCategoryListRequestValidator
    : AbstractValidator<AdminVideoCategoryListRequest>
{
    /// <summary>
    /// 初始化管理员分类列表分页和关键词规则。
    /// </summary>
    public AdminVideoCategoryListRequestValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
    }
}

/// <summary>
/// 提供视频请求共享的分类数量限制。
/// </summary>
public static class VideoConstraints
{
    public const int MaxCategoryCount = 10;
}
