namespace TinyLang.Dtos;

/// <summary>
/// 描述向新邮箱发送换绑验证码的请求。
/// </summary>
public sealed record SendChangeEmailTokenRequest
{
    public required string NewEmail { get; init; }
}

/// <summary>
/// 描述匿名申请密码找回验证码的邮箱。
/// </summary>
public sealed record ForgotPasswordTokenRequest
{
    public required string Email { get; init; }
}

/// <summary>
/// 描述匿名使用邮箱验证码重置密码的请求。
/// </summary>
public sealed record ForgotPasswordRequest
{
    public required string Email { get; init; }
    public required string NewPassword { get; init; }
    public required string VerificationCode { get; init; }
}

/// <summary>
/// 描述使用验证码重置当前账户密码的请求。
/// </summary>
public sealed record ResetPasswordRequest
{
    public required string NewPassword { get; init; }
    public required string VerificationCode { get; init; }
}

/// <summary>
/// 描述使用验证码完成邮箱换绑的请求。
/// </summary>
public sealed record ChangeEmailRequest
{
    public required string NewEmail { get; init; }
    public required string VerificationCode { get; init; }
}

/// <summary>
/// 描述使用验证码删除当前账户的请求。
/// </summary>
public sealed record DeleteAccountRequest
{
    public required string VerificationCode { get; init; }
}

/// <summary>
/// 返回邮箱换绑后的用户标识和规范化邮箱。
/// </summary>
/// <param name="Id">用户标识。</param>
/// <param name="Email">换绑后的邮箱。</param>
public sealed record ChangeEmailResponse(Guid Id, string Email);
