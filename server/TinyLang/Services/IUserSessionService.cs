using TinyLang.Entities;

namespace TinyLang.Services;

/// <summary>
/// 定义通过 token version 和 refresh token 记录使用户会话失效的契约。
/// </summary>
public interface IUserSessionService
{
    /// <summary>
    /// 递增用户 token version，并撤销其所有有效 refresh token。
    /// </summary>
    /// <param name="user">需要失效全部会话的已跟踪用户。</param>
    /// <param name="cancellationToken">用于取消数据库查询的令牌。</param>
    /// <returns>表示异步会话失效操作的任务；调用方负责保存上下文变更。</returns>
    Task InvalidateAllAsync(User user, CancellationToken cancellationToken = default);
}
