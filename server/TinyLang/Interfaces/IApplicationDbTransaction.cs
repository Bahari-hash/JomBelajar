namespace TinyLang.Interfaces;

/// <summary>
/// 定义应用服务提交一个显式数据库事务所需的最小契约。
/// </summary>
public interface IApplicationDbTransaction : IAsyncDisposable
{
    /// <summary>
    /// 提交事务中已经持久化的全部变更。
    /// </summary>
    /// <param name="cancellationToken">用于取消提交操作的令牌。</param>
    /// <returns>表示异步提交操作的任务。</returns>
    Task CommitAsync(CancellationToken cancellationToken = default);
}
