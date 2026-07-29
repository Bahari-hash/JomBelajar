using FluentValidation;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 校验用户资料字段的长度和头像 URL 格式。
/// </summary>
public sealed class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    /// <summary>
    /// 初始化用户资料更新请求的校验规则。
    /// </summary>
    public UpdateProfileRequestValidator()
    {
        RuleFor(x => x.Nickname)
            .MaximumLength(60).WithErrKey(ErrorCodes.NicknameLengthLimit);

        RuleFor(x => x.Bio)
            .MaximumLength(500).WithErrKey(ErrorCodes.BioLengthLimit);

        RuleFor(x => x.AvatarUrl)
            .MaximumLength(500).WithErrKey(ErrorCodes.AvatarUrlLengthLimit)
            .Must(BeValidAvatarUrl).WithErrKey(ErrorCodes.AvatarUrlFormatInvalid);
    }

    /// <summary>
    /// 判断头像地址是否为空或为绝对 HTTP/HTTPS URL。
    /// </summary>
    /// <param name="value">待校验的头像地址。</param>
    /// <returns>地址为空或格式受支持时返回 <see langword="true"/>。</returns>
    private static bool BeValidAvatarUrl(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
    }
}

/// <summary>
/// 校验管理员提交的用户角色名称。
/// </summary>
public sealed class UpdateRoleRequestValidator : AbstractValidator<UpdateRoleRequest>
{
    /// <summary>
    /// 初始化用户角色更新请求的校验规则。
    /// </summary>
    public UpdateRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .NotEmpty().WithErrKey(ErrorCodes.RoleRequired)
            .Must(BeValidRole).WithErrKey(ErrorCodes.RoleInvalid);
    }

    /// <summary>
    /// 判断角色名称能否映射到已定义的 <see cref="UserRole"/> 值。
    /// </summary>
    /// <param name="role">待解析的角色名称。</param>
    /// <returns>角色名称有效时返回 <see langword="true"/>。</returns>
    private static bool BeValidRole(string role)
        => Enum.TryParse<UserRole>(role, ignoreCase: true, out var parsed) &&
            Enum.IsDefined(parsed);
}

/// <summary>
/// 校验管理员用户列表的分页、关键词、角色和状态筛选。
/// </summary>
public sealed class AdminUserListRequestValidator
    : AbstractValidator<AdminUserListRequest>
{
    /// <summary>
    /// 初始化管理员用户列表筛选规则。
    /// </summary>
    public AdminUserListRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithErrKey(ErrorCodes.PageInvalid);
        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithErrKey(ErrorCodes.PageSizeInvalid);
        RuleFor(x => x.Keyword)
            .Must(value => value is null || value.Trim().Length <= 200)
            .WithErrKey(ErrorCodes.KeywordLengthLimit)
            .Must(value => string.IsNullOrEmpty(value) || !value.Any(char.IsControl))
            .WithErrKey(ErrorCodes.KeywordInvalid);
        RuleFor(x => x.Role)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.RoleInvalid);
        RuleFor(x => x.Status)
            .Must(value => value is null || Enum.IsDefined(value.Value))
            .WithErrKey(ErrorCodes.RequestValidationFailed);
    }
}

/// <summary>
/// 校验管理员提交的用户封禁原因。
/// </summary>
public sealed class BanUserRequestValidator : AbstractValidator<BanUserRequest>
{
    /// <summary>
    /// 初始化封禁原因的必填和 trim 后长度规则。
    /// </summary>
    public BanUserRequestValidator()
    {
        RuleFor(x => x.Reason)
            .Cascade(CascadeMode.Stop)
            .Must(value => !string.IsNullOrWhiteSpace(value))
            .WithErrKey(ErrorCodes.BanUserReasonRequired)
            .Must(value => value.Trim().Length <= 500)
            .WithErrKey(ErrorCodes.BanUserReasonLengthLimit);
    }
}
