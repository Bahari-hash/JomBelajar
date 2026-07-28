namespace TinyLang.Entities;

/// <summary>
/// 表示视频与视频分类之间的多对多关联。
/// </summary>
public sealed class VideoCategoryAssignment
{
    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;

    public Guid VideoCategoryId { get; set; }
    public VideoCategory VideoCategory { get; set; } = null!;
}
