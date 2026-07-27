using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 保存一个用户对一个视频的幂等续播位置和服务端完成状态。
/// </summary>
public sealed class UserVideoProgress : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识的播放进度记录。
    /// </summary>
    public UserVideoProgress()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public double PositionSeconds { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset LastPlayedAt { get; set; }
}
