using TinyLang.Entities;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义 PostgreSQL 原子写入用户视频进度的窄持久化边界。
/// </summary>
public interface IUserVideoProgressStore
{
    /// <summary>
    /// 以用户和视频唯一键插入或更新播放进度，避免首次写入竞争。
    /// </summary>
    /// <param name="userId">经过认证的当前用户标识。</param>
    /// <param name="videoId">已发布且就绪的视频标识。</param>
    /// <param name="positionSeconds">经过服务端验证的位置。</param>
    /// <param name="isCompleted">服务端计算的完成状态。</param>
    /// <param name="now">当前 UTC 时间。</param>
    /// <param name="cancellationToken">用于取消数据库写入的令牌。</param>
    /// <returns>原子写入后的进度。</returns>
    Task<UserVideoProgress> UpsertAsync(
        Guid userId,
        Guid videoId,
        double positionSeconds,
        bool isCompleted,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);
}
