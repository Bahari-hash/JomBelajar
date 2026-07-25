namespace TinyLang.Models;

/// <summary>
/// 表示已去除 provider 响应细节的对象存储协议失败。
/// </summary>
public sealed class ObjectStorageProtocolException : Exception
{
    /// <summary>
    /// 使用稳定协议原因创建不包含第三方错误正文的异常。
    /// </summary>
    /// <param name="reason">可由业务层安全映射的协议失败原因。</param>
    public ObjectStorageProtocolException(ObjectStorageProtocolError reason)
        : base("Object storage protocol operation failed.")
    {
        Reason = reason;
    }

    public ObjectStorageProtocolError Reason { get; }
}

/// <summary>
/// 定义业务层需要区分的对象存储协议失败类别。
/// </summary>
public enum ObjectStorageProtocolError
{
    UploadNotFound,
    InvalidParts
}
