namespace TinyLang.Models;

/// <summary>
/// 定义可持久化和向管理员展示的稳定视频处理失败分类。
/// </summary>
public enum VideoProcessingFailureCode
{
    ProbeFailed,
    VideoStreamMissing,
    AudioStreamMissing,
    DurationInvalid,
    DisplayDimensionsInvalid,
    SourceResolutionExceeded,
    ProcessStartFailed,
    ProcessTimedOut,
    TranscodeFailed,
    SourceDownloadFailed,
    OutputUploadFailed,
    OutputValidationFailed,
    TemporaryStorageUnavailable,
    WorkerUnexpectedFailure
}

/// <summary>
/// 表示经过清理并带有自动重试语义的视频处理失败。
/// </summary>
public sealed class VideoProcessingException : Exception
{
    /// <summary>
    /// 创建仅携带稳定失败码和重试分类的媒体处理异常。
    /// </summary>
    /// <param name="failureCode">可安全持久化的失败码。</param>
    /// <param name="isTransient">是否允许 worker 自动重试。</param>
    public VideoProcessingException(
        VideoProcessingFailureCode failureCode,
        bool isTransient)
        : base($"Video processing failed with code {failureCode}.")
    {
        FailureCode = failureCode;
        IsTransient = isTransient;
    }

    public VideoProcessingFailureCode FailureCode { get; }
    public bool IsTransient { get; }
}
