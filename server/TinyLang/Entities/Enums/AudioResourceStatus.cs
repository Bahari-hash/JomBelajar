namespace TinyLang.Entities.Enums;

/// <summary>
/// 表示共享音频资源当前所处的上传和处理阶段。
/// </summary>
public enum AudioResourceStatus
{
    Uploading,
    Queued,
    Processing,
    Ready,
    Failed
}
