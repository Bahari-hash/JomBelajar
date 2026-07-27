using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 描述视频某个输出版本中实际生成的一条 HLS 清晰度。
/// </summary>
public sealed class VideoRendition : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的清晰度记录。
    /// </summary>
    public VideoRendition()
    {
        Id = Guid.NewGuid();
    }

    public Guid VideoId { get; set; }
    public Video Video { get; set; } = null!;
    public Guid OutputVersion { get; set; }
    public int TargetHeight { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int VideoBitrateKbps { get; set; }
    public int AudioBitrateKbps { get; set; }
    public required string PlaylistObjectName { get; set; }
    public required string Codecs { get; set; }
}
