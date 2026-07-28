using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示全局共享且可由管理员维护的视频分类。
/// </summary>
public sealed class VideoCategory : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识且默认启用的视频分类。
    /// </summary>
    public VideoCategory()
    {
        Id = Guid.NewGuid();
    }

    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<VideoCategoryAssignment> VideoAssignments { get; set; } = [];
}
