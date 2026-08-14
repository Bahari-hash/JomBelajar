using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述申请注册验证码的请求。
/// </summary>
public sealed record RegisterTokenRequest
{
    public required string Email { get; init; }
}

/// <summary>
/// 描述使用邮箱验证码创建账户的请求。
/// </summary>
public sealed record RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string VerificationCode { get; init; }
}

/// <summary>
/// 描述使用邮箱和密码登录的请求。
/// </summary>
public sealed record LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

/// <summary>
/// 返回认证流程所需的用户基本身份信息。
/// </summary>
/// <param name="Id">用户标识。</param>
/// <param name="Email">用户邮箱。</param>
/// <param name="Role">用户当前角色。</param>
public sealed record UserResponse(Guid Id, string Email, UserRole Role);

/// <summary>
/// 返回 access token、refresh token 及当前用户信息。
/// </summary>
/// <param name="Token">JWT access token。</param>
/// <param name="RefreshToken">用于续期会话的 refresh token。</param>
/// <param name="ExpiresIn">access token 剩余有效秒数。</param>
/// <param name="User">令牌所属用户。</param>
public sealed record AuthTokenIssueResult(
    string Token,
    string RefreshToken,
    long ExpiresIn,
    UserResponse User);

/// <summary>
/// 返回浏览器可见的认证会话，不包含 refresh token。
/// </summary>
public sealed record AuthSessionResponse(
    string Token,
    long ExpiresIn,
    UserResponse User);
