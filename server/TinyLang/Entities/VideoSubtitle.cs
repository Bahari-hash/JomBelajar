using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示视频关联的一条经基础内容验证的 WebVTT 字幕。
/// </summary>
public sealed class VideoSubtitle : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识的字幕关联。
    /// </summary>
    public VideoSubtitle()
    {
        Id = Guid.NewGuid();
    }

    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public Guid MediaResourceId { get; set; }
    public MediaResource MediaResource { get; set; } = null!;
    public required string LanguageTag { get; set; }
    public required string DisplayName { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
