using TinyLang.Enums;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义验证码生成、存储、发送和校验的编排契约。
/// </summary>
public interface IVerificationCodeSender
{
    /// <summary>
    /// 为指定邮箱和用途生成验证码，保存后排队发送邮件。
    /// </summary>
    /// <param name="email">接收验证码的邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步发送编排的任务。</returns>
    Task SendCodeAsync(string email, VerificationCodePurpose purpose, CancellationToken cancellationToken = default);

    /// <summary>
    /// 验证并消费指定邮箱和用途的验证码。
    /// </summary>
    /// <param name="email">验证码所属邮箱。</param>
    /// <param name="purpose">验证码授权的操作。</param>
    /// <param name="code">客户端提交的验证码。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>验证码匹配且成功消费时返回 <see langword="true"/>。</returns>
    Task<bool> VerifyCodeAsync(string email, VerificationCodePurpose purpose, string code, CancellationToken cancellationToken = default);
}
