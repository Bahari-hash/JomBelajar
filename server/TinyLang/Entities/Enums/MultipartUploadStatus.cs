namespace TinyLang.Entities.Enums;

/// <summary>
/// 表示 S3 Multipart Upload 会话的协议和后台处理状态。
/// </summary>
public enum MultipartUploadStatus
{
    Initiated,
    Completing,
    Finalizing,
    Completed,
    Aborting,
    Aborted,
    Expired,
    Failed
}
