namespace TinyLang.Entities.Enums;

/// <summary>
/// 表示持久化音频处理任务的调度状态。
/// </summary>
public enum AudioProcessingJobStatus
{
    Queued,
    Processing,
    Completed,
    Failed
}
