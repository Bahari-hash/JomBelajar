namespace TinyLang.Entities.Enums;

/// <summary>
/// 表示视频当前媒体处理阶段。
/// </summary>
public enum VideoProcessingStatus
{
    Queued,
    Processing,
    Ready,
    Failed
}
