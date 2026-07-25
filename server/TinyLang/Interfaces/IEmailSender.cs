using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义同步投递或通过消息队列排队发送邮件的应用契约。
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// 立即将邮件交给底层邮件 provider 投递。
    /// </summary>
    /// <param name="message">待发送的邮件。</param>
    /// <param name="cancellationToken">用于取消发送操作的令牌。</param>
    /// <returns>表示异步发送操作的任务。</returns>
    Task SendEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);

    /// <summary>
    /// 将邮件发布到消息队列以便后台投递。
    /// </summary>
    /// <param name="message">待排队发送的邮件。</param>
    /// <param name="cancellationToken">用于取消发布操作的令牌。</param>
    /// <returns>表示异步发布操作的任务。</returns>
    Task EnqueueEmailAsync(EmailMessage message, CancellationToken cancellationToken = default);
}
