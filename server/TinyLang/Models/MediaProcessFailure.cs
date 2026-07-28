namespace TinyLang.Models;

/// <summary>
/// 定义媒体进程执行器可报告的基础设施失败类别。
/// </summary>
public enum MediaProcessFailureKind
{
    StartFailed,
    TimedOut
}

/// <summary>
/// 表示与具体媒体业务无关的外部进程启动或超时失败。
/// </summary>
public sealed class MediaProcessException : Exception
{
    /// <summary>
    /// 使用基础设施失败类别创建媒体进程异常。
    /// </summary>
    public MediaProcessException(MediaProcessFailureKind failureKind)
        : base($"Media process failed with kind {failureKind}.")
    {
        FailureKind = failureKind;
    }

    public MediaProcessFailureKind FailureKind { get; }
}
