using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义向外部邮件服务器投递完整邮件消息的契约。
/// </summary>
public interface IEmailProvider
{
    /// <summary>
    /// 将包装后的邮件消息投递到已配置的邮件服务器。
    /// </summary>
    /// <param name="message">包含消息标识和创建时间的邮件。</param>
    /// <param name="cancellationToken">用于取消投递操作的令牌。</param>
    /// <returns>表示异步投递操作的任务。</returns>
    Task DeliverEmailAsync(EmailMessageWrapper message, CancellationToken cancellationToken = default);
}
