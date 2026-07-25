using FluentValidation;
using Microsoft.Extensions.Options;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Extensions;
using TinyLang.Policies;
using TinyLang.Settings;

namespace TinyLang.Dtos;

/// <summary>
/// 根据头像上传策略校验文件元数据。
/// </summary>
public sealed class AvatarPresignRequestValidator : AbstractValidator<AvatarPresignRequest>
{
    /// <summary>
    /// 使用指定上传策略初始化头像预签名请求的校验规则。
    /// </summary>
    /// <param name="policy">媒体上传限制策略。</param>
    public AvatarPresignRequestValidator(MediaUploadPolicy policy)
    {
        AddFileMetadataRules();

        RuleFor(x => x.Extension)
            .Must(extension => policy.IsExtensionAllowed(ResourceModule.Avatar, extension))
            .WithErrKey(ErrorCodes.MediaExtensionInvalid);

        RuleFor(x => x.ContentType)
            .Must((request, contentType) => policy.IsContentTypeAllowed(
                ResourceModule.Avatar,
                request.Extension,
                contentType))
            .WithErrKey(ErrorCodes.MediaContentTypeInvalid);

        RuleFor(x => x.Size)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithErrKey(ErrorCodes.MediaSizeInvalid)
            .Must(size => policy.IsSizeAllowed(ResourceModule.Avatar, size))
            .WithErrKey(ErrorCodes.MediaSizeLimitExceeded);
    }

    /// <summary>
    /// 添加头像文件名、扩展名和媒体类型的通用元数据规则。
    /// </summary>
    private void AddFileMetadataRules()
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
            .MaximumLength(16).WithErrKey(ErrorCodes.MediaExtensionInvalid);

        RuleFor(x => x.Extension)
            .Must((request, extension) => MediaUploadPolicy.ExtensionMatchesName(
                request.OriginalName,
                extension))
            .WithErrKey(ErrorCodes.MediaExtensionMismatch);

        RuleFor(x => x.ContentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaContentTypeRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.MediaContentTypeInvalid);
    }
}

/// <summary>
/// 根据编辑者媒体模块及上传策略校验文件元数据。
/// </summary>
public sealed class EditorMediaPresignRequestValidator : AbstractValidator<EditorMediaPresignRequest>
{
    /// <summary>
    /// 使用指定上传策略初始化编辑者媒体预签名请求的校验规则。
    /// </summary>
    /// <param name="policy">媒体上传限制策略。</param>
    public EditorMediaPresignRequestValidator(MediaUploadPolicy policy)
    {
        RuleFor(x => x.OriginalName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaOriginalNameRequired)
            .MaximumLength(255).WithErrKey(ErrorCodes.MediaOriginalNameLengthLimit)
            .Must(MediaUploadPolicy.IsSafeFileName)
            .WithErrKey(ErrorCodes.MediaOriginalNameInvalid);

        RuleFor(x => x.Module)
            .Must(policy.IsEditorModule)
            .WithErrKey(ErrorCodes.ResourceModuleInvalid);

        RuleFor(x => x.Extension)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaExtensionRequired)
            .MaximumLength(16).WithErrKey(ErrorCodes.MediaExtensionInvalid)
            .Must((request, extension) =>
                !policy.IsSupportedModule(request.Module) ||
                policy.IsExtensionAllowed(request.Module, extension))
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
            .Must((request, contentType) =>
                !policy.IsSupportedModule(request.Module) ||
                policy.IsContentTypeAllowed(
                    request.Module,
                    request.Extension,
                    contentType))
            .WithErrKey(ErrorCodes.MediaContentTypeInvalid);

        RuleFor(x => x.Size)
            .Cascade(CascadeMode.Stop)
            .GreaterThan(0).WithErrKey(ErrorCodes.MediaSizeInvalid)
            .Must((request, size) =>
                !policy.IsSupportedModule(request.Module) ||
                policy.IsSizeAllowed(request.Module, size))
            .WithErrKey(ErrorCodes.MediaSizeLimitExceeded);
    }
}

/// <summary>
/// 校验编辑者 Multipart Upload 初始化请求的媒体元数据和阈值。
/// </summary>
public sealed class MultipartUploadRequestValidator : AbstractValidator<MultipartUploadRequest>
{
    /// <summary>
    /// 使用媒体策略和 Multipart 配置建立初始化请求规则。
    /// </summary>
    public MultipartUploadRequestValidator(
        MediaUploadPolicy policy,
        IOptions<MultipartUploadSettings> multipartOptions)
    {
        var threshold = (long)multipartOptions.Value.ThresholdMB * 1024 * 1024;

        RuleFor(x => x.OriginalName)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaOriginalNameRequired)
            .MaximumLength(255).WithErrKey(ErrorCodes.MediaOriginalNameLengthLimit)
            .Must(MediaUploadPolicy.IsSafeFileName)
            .WithErrKey(ErrorCodes.MediaOriginalNameInvalid);

        RuleFor(x => x.Module)
            .Must(policy.IsEditorModule)
            .WithErrKey(ErrorCodes.ResourceModuleInvalid);

        RuleFor(x => x.Extension)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaExtensionRequired)
            .MaximumLength(16).WithErrKey(ErrorCodes.MediaExtensionInvalid)
            .Must((request, extension) =>
                !policy.IsSupportedModule(request.Module) ||
                policy.IsExtensionAllowed(request.Module, extension))
            .WithErrKey(ErrorCodes.MediaExtensionInvalid)
            .Must((request, extension) => MediaUploadPolicy.ExtensionMatchesName(
                request.OriginalName,
                extension))
            .WithErrKey(ErrorCodes.MediaExtensionMismatch);

        RuleFor(x => x.ContentType)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithErrKey(ErrorCodes.MediaContentTypeRequired)
            .MaximumLength(100).WithErrKey(ErrorCodes.MediaContentTypeInvalid)
            .Must((request, contentType) =>
                !policy.IsSupportedModule(request.Module) ||
                policy.IsContentTypeAllowed(
                    request.Module,
                    request.Extension,
                    contentType))
            .WithErrKey(ErrorCodes.MediaContentTypeInvalid);

        RuleFor(x => x.Size)
            .Cascade(CascadeMode.Stop)
            .GreaterThanOrEqualTo(threshold)
            .WithErrKey(ErrorCodes.MultipartUploadNotRequired)
            .Must((request, size) =>
                !policy.IsSupportedModule(request.Module) ||
                policy.IsSizeAllowed(request.Module, size))
            .WithErrKey(ErrorCodes.MediaSizeLimitExceeded);
    }
}

/// <summary>
/// 校验一次批量 part 预签名请求的数量、唯一性和正数约束。
/// </summary>
public sealed class MultipartPartPresignRequestValidator
    : AbstractValidator<MultipartPartPresignRequest>
{
    /// <summary>
    /// 使用配置的批量上限建立 part number 规则。
    /// </summary>
    public MultipartPartPresignRequestValidator(
        IOptions<MultipartUploadSettings> multipartOptions)
    {
        RuleFor(x => x.PartNumbers)
            .NotEmpty().WithErrKey(ErrorCodes.MultipartUploadPartsInvalid)
            .Must(parts => parts.Count <= multipartOptions.Value.PartPresignBatchLimit)
            .WithErrKey(ErrorCodes.MultipartUploadPartsInvalid)
            .Must(parts => parts.Distinct().Count() == parts.Count)
            .WithErrKey(ErrorCodes.MultipartUploadPartsInvalid);
        RuleForEach(x => x.PartNumbers)
            .GreaterThan(0).WithErrKey(ErrorCodes.MultipartUploadPartsInvalid);
    }
}

/// <summary>
/// 校验 Multipart complete 请求中 part 编号和 ETag 的基础结构。
/// </summary>
public sealed class CompleteMultipartUploadRequestValidator
    : AbstractValidator<CompleteMultipartUploadRequest>
{
    /// <summary>
    /// 使用配置的最大 part 数建立 complete 请求规则。
    /// </summary>
    public CompleteMultipartUploadRequestValidator(
        IOptions<MultipartUploadSettings> multipartOptions)
    {
        RuleFor(x => x.Parts)
            .NotEmpty().WithErrKey(ErrorCodes.MultipartUploadPartsInvalid)
            .Must(parts => parts.Count <= multipartOptions.Value.MaxPartCount)
            .WithErrKey(ErrorCodes.MultipartUploadPartsInvalid)
            .Must(parts => parts.Select(part => part.PartNumber).Distinct().Count() == parts.Count)
            .WithErrKey(ErrorCodes.MultipartUploadPartsInvalid);
        RuleForEach(x => x.Parts).ChildRules(part =>
        {
            part.RuleFor(x => x.PartNumber)
                .GreaterThan(0).WithErrKey(ErrorCodes.MultipartUploadPartsInvalid);
            part.RuleFor(x => x.ETag)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithErrKey(ErrorCodes.MultipartUploadPartsInvalid)
                .MaximumLength(256).WithErrKey(ErrorCodes.MultipartUploadPartsInvalid)
                .Must(value => !value.Any(char.IsControl))
                .WithErrKey(ErrorCodes.MultipartUploadPartsInvalid);
        });
    }
}
