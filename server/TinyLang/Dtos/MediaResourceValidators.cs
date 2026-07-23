using FluentValidation;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Extensions;
using TinyLang.Policies;

namespace TinyLang.Dtos;

public sealed class AvatarPresignRequestValidator : AbstractValidator<AvatarPresignRequest>
{
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

public sealed class EditorMediaPresignRequestValidator : AbstractValidator<EditorMediaPresignRequest>
{
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
