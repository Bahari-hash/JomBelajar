using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义当前用户执行敏感账户安全操作的业务契约。
/// </summary>
public interface IAccountSecurityService
{
    /// <summary>
    /// 验证重置码并替换当前用户密码，同时撤销现有会话。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="request">新密码和验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步重置操作的任务。</returns>
    Task ResetPasswordAsync(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用邮箱验证码匿名重置密码，并撤销该账户的全部会话。
    /// </summary>
    /// <param name="request">邮箱、新密码和验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步重置操作的任务。</returns>
    Task ResetForgottenPasswordAsync(
        ForgotPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证换绑码并更新当前用户邮箱，同时撤销现有会话。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="request">新邮箱和验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>邮箱更新后的用户摘要。</returns>
    Task<ChangeEmailResponse> ChangeEmailAsync(
        Guid userId,
        ChangeEmailRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证删除码并软删除当前账户，同时撤销现有会话。
    /// </summary>
    /// <param name="userId">当前用户标识。</param>
    /// <param name="request">账户删除验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步删除操作的任务。</returns>
    Task DeleteAccountAsync(
        Guid userId,
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default);
}
