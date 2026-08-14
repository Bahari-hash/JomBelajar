using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

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
