using TinyLang.Entities;

namespace TinyLang.Services;

/// <summary>
/// 定义通过 token version 和 refresh token 记录使用户会话失效的契约。
/// </summary>
public interface IUserSessionService
{
    /// <summary>
    /// 在同一事务中保存用户的已跟踪变更、递增 token version，
    /// 并撤销其所有尚未撤销的 refresh token。
    /// </summary>
    /// <param name="user">需要失效全部会话的已跟踪用户。</param>
    /// <param name="cancellationToken">用于取消数据库查询的令牌。</param>
    /// <returns>表示异步会话失效和持久化操作的任务。</returns>
    Task InvalidateAllAsync(User user, CancellationToken cancellationToken = default);
}
