namespace TinyLang.Entities.Enums;

/// <summary>
/// 表示音频当前媒体处理阶段。
/// </summary>
public enum AudioProcessingStatus
{
    Queued,
    Processing,
    Ready,
    Failed
}
