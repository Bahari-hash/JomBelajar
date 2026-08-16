using FluentValidation;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Extensions;
using TinyLang.Policies;

namespace TinyLang.Dtos;

/// <summary>
/// 根据音频模块上传策略校验初始化请求的文件元数据。
/// </summary>
public sealed class InitializeAudioUploadRequestValidator
    : AbstractValidator<InitializeAudioUploadRequest>
{
    public InitializeAudioUploadRequestValidator(
        MediaUploadPolicy policy)
    {
        RuleFor(x => x.OriginalName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaOriginalNameRequired)
            .MaximumLength(255).WithErrKey(ErrorCodes.MediaOriginalNameLengthLimit)
            .Must(MediaUploadPolicy.IsSafeFileName)
            .WithErrKey(ErrorCodes.MediaOriginalNameInvalid);

        RuleFor(x => x.Extension)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaExtensionRequired)
            .MaximumLength(16).WithErrKey(ErrorCodes.MediaExtensionInvalid)
            .Must(extension => policy.IsExtensionAllowed(ResourceModule.Audio, extension))
            .WithErrKey(ErrorCodes.MediaExtensionInvalid);

        RuleFor(x => x.Extension)
            .Must((request, extension) => MediaUploadPolicy.ExtensionMatchesName(
                request.OriginalName,
                extension))
            .WithErrKey(ErrorCodes.MediaExtensionMismatch);

        RuleFor(x => x.ContentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaContentTypeRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.MediaContentTypeInvalid)
            .Must((request, contentType) => policy.IsContentTypeAllowed(
                ResourceModule.Audio,
                request.Extension,
                contentType))
            .WithErrKey(ErrorCodes.MediaContentTypeInvalid);

        RuleFor(x => x.Size)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithErrKey(ErrorCodes.MediaSizeInvalid)
            .Must(size => policy.IsSizeAllowed(ResourceModule.Audio, size))
            .WithErrKey(ErrorCodes.MediaSizeLimitExceeded);
    }
}

/// <summary>
/// 校验管理员重命名音频资源的显示名称。
/// </summary>
public sealed class RenameAudioResourceRequestValidator
    : AbstractValidator<RenameAudioResourceRequest>
{
    public RenameAudioResourceRequestValidator()
    {
        RuleFor(value => value.Name)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.AudioTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.AudioTitleLengthLimit)
            .Must(value => value.All(character => !char.IsControl(character)))
            .WithErrKey(ErrorCodes.AudioTitleRequired);
    }
}

/// <summary>
/// 校验管理员音频资源分页和状态筛选。
/// </summary>
public sealed class AdminAudioResourceListRequestValidator
    : AbstractValidator<AdminAudioResourceListRequest>
{
    public AdminAudioResourceListRequestValidator()
    {
        RuleFor(value => value.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(value => value.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(value => value.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit);
        RuleFor(value => value.Status)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.AudioStatusConflict);
    }
}

/// <summary>
/// 校验创建音频资产请求中的源资源和展示元数据。
/// </summary>
public sealed class CreateAudioClipRequestValidator
    : AbstractValidator<CreateAudioClipRequest>
{
    /// <summary>
    /// 创建音频源标识、标题、简介、语言和用途规则。
    /// </summary>
    public CreateAudioClipRequestValidator()
    {
        RuleFor(value => value.SourceMediaResourceId)
            .NotEmpty().WithErrKey(ErrorCodes.AudioSourceInvalid);
        RuleFor(value => value.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.AudioTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.AudioTitleLengthLimit);
        RuleFor(value => value.Description)
            .MaximumLength(2000).WithErrKey(ErrorCodes.AudioDescriptionLengthLimit);
        RuleFor(value => value.Kind)
            .Must(Enum.IsDefined)
            .WithErrKey(ErrorCodes.AudioKindInvalid);
    }
}

/// <summary>
/// 校验音频展示元数据更新请求。
/// </summary>
public sealed class UpdateAudioClipRequestValidator
    : AbstractValidator<UpdateAudioClipRequest>
{
    /// <summary>
    /// 创建音频标题、简介、语言和用途规则。
    /// </summary>
    public UpdateAudioClipRequestValidator()
    {
        RuleFor(value => value.Title)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.AudioTitleRequired)
            .MaximumLength(200).WithErrKey(ErrorCodes.AudioTitleLengthLimit);
        RuleFor(value => value.Description)
            .MaximumLength(2000).WithErrKey(ErrorCodes.AudioDescriptionLengthLimit);
        RuleFor(value => value.Kind)
            .Must(Enum.IsDefined)
            .WithErrKey(ErrorCodes.AudioKindInvalid);
    }
}

/// <summary>
/// 校验管理员音频列表的分页、关键词和枚举筛选。
/// </summary>
public sealed class AdminAudioClipListRequestValidator
    : AbstractValidator<AdminAudioClipListRequest>
{
    /// <summary>
    /// 创建有界分页、关键词和显式枚举规则。
    /// </summary>
    public AdminAudioClipListRequestValidator()
    {
        RuleFor(value => value.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(value => value.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(value => value.Keyword)
            .MaximumLength(200).WithErrKey(ErrorCodes.KeywordLengthLimit);
        RuleFor(value => value.ProcessingStatus)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.AudioProcessingStatusInvalid);
        RuleFor(value => value.PublicationStatus)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.AudioPublicationStatusInvalid);
        RuleFor(value => value.Kind)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.AudioKindInvalid);
    }
}
