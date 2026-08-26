using TinyLang.Dtos;
using TinyLang.Enums;

namespace TinyLang.Services;

/// <summary>
/// 定义账户注册、登录、令牌续期、退出和验证码发送的认证业务契约。
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 向未注册邮箱发送账户注册验证码。
    /// </summary>
    /// <param name="email">待注册邮箱。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    Task SendRegisterTokenAsync(string email, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证注册验证码并创建用户账户。
    /// </summary>
    /// <param name="request">邮箱、密码和注册验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>新建用户的认证摘要。</returns>
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证邮箱密码并签发新的 access token 和 refresh token。
    /// </summary>
    /// <param name="request">登录凭据。</param>
    /// <param name="clientIp">客户端 IP 地址。</param>
    /// <param name="deviceInfo">客户端设备描述。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>新签发的令牌及用户信息。</returns>
    Task<AuthTokenIssueResult> LoginAsync(LoginRequest request, string? clientIp, string? deviceInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证并轮换 refresh token，签发新的令牌对。
    /// </summary>
    /// <param name="refreshToken">当前 refresh token。</param>
    /// <param name="clientIp">本次续期的客户端 IP 地址。</param>
    /// <param name="deviceInfo">本次续期的设备描述。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>轮换后的令牌及用户信息。</returns>
    Task<AuthTokenIssueResult> RefreshAsync(string? refreshToken, string? clientIp, string? deviceInfo, CancellationToken cancellationToken = default);

    /// <summary>
    /// 撤销当前 access token，并在提供 refresh token 时撤销对应会话。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="accessTokenId">当前 JWT token 标识。</param>
    /// <param name="accessTokenExpiresAt">当前 access token 过期时间。</param>
    /// <param name="refreshToken">可选的当前 refresh token。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步撤销操作的任务。</returns>
    Task LogoutAsync(
        Guid userId,
        string accessTokenId,
        DateTimeOffset accessTokenExpiresAt,
        string? refreshToken,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 撤销指定用户的全部现有会话。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步撤销操作的任务。</returns>
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向未占用的新邮箱发送换绑验证码。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="newEmail">目标邮箱。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    Task SendChangeEmailTokenAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default);

    /// <summary>
    /// 向当前用户邮箱发送密码重置验证码。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    Task SendResetPasswordTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 为有效账户发送匿名密码找回验证码；未知邮箱保持静默成功。
    /// </summary>
    /// <param name="email">待找回账户的邮箱。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    Task SendForgotPasswordTokenAsync(
        string email,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 向当前用户邮箱发送账户删除验证码。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    Task SendDeleteAccountTokenAsync(Guid userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证并消费指定用途的验证码。
    /// </summary>
    /// <param name="email">验证码所属邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <param name="code">客户端提交的验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>验证码匹配且成功消费时返回 <see langword="true"/>。</returns>
    Task<bool> VerifyCodeAsync(string email, VerificationCodePurpose purpose, string code, CancellationToken cancellationToken = default);
}
