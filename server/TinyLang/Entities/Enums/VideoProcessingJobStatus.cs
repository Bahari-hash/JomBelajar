namespace TinyLang.Entities.Enums;

/// <summary>
/// 表示持久化视频处理任务的调度状态。
/// </summary>
public enum VideoProcessingJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
