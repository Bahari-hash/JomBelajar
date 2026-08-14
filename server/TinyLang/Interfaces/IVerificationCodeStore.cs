using TinyLang.Enums;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义验证码的临时保存、读取和原子消费契约。
/// </summary>
public interface IVerificationCodeStore
{
    /// <summary>
    /// 保存指定邮箱和用途的验证码，并应用配置的有效期。
    /// </summary>
    /// <param name="email">验证码所属邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <param name="code">待保存的验证码。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>表示异步保存操作的任务。</returns>
    Task SaveAsync(
        string email,
        VerificationCodePurpose purpose,
        string code,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 读取指定邮箱和用途当前尚未过期的验证码。
    /// </summary>
    /// <param name="email">验证码所属邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>验证码；不存在或已过期时返回 <see langword="null"/>。</returns>
    Task<string?> GetAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 仅在存储值匹配时原子删除验证码。
    /// </summary>
    /// <param name="email">验证码所属邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <param name="expectedValue">客户端提交的期望验证码。</param>
    /// <param name="cancellationToken">用于取消缓存操作的令牌。</param>
    /// <returns>验证码匹配并成功删除时返回 <see langword="true"/>。</returns>
    Task<bool> TryConsumeAsync(
        string email,
        VerificationCodePurpose purpose,
        string expectedValue,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子递增指定邮箱和用途的验证码失败次数。
    /// </summary>
    Task<int> IncrementFailureAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清除指定验证码的失败次数。
    /// </summary>
    Task ResetFailuresAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等删除指定验证码，使其无法继续消费。
    /// </summary>
    Task DeleteAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default);
}
