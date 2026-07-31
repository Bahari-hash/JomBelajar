namespace TinyLang.Models;

/// <summary>
/// 定义可持久化和向管理员展示的稳定音频处理失败分类。
/// </summary>
public enum AudioProcessingFailureCode
{
    ProbeFailed,
    AudioStreamMissing,
    VideoStreamPresent,
    DurationInvalid,
    SourceDurationExceeded,
    SampleRateInvalid,
    SourceSampleRateExceeded,
    ChannelsInvalid,
    SourceChannelsExceeded,
    SourceResourceInvalid,
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
/// 表示经过清理并带有自动重试语义的音频处理失败。
/// </summary>
public sealed class AudioProcessingException : Exception
{
    /// <summary>
    /// 创建仅携带稳定失败码和重试分类的音频处理异常。
    /// </summary>
    public AudioProcessingException(
        AudioProcessingFailureCode failureCode,
        bool isTransient)
        : base($"Audio processing failed with code {failureCode}.")
    {
        FailureCode = failureCode;
        IsTransient = isTransient;
    }

    public AudioProcessingFailureCode FailureCode { get; }
    public bool IsTransient { get; }
}
