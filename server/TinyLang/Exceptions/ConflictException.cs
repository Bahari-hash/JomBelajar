using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示资源状态或唯一性冲突，并映射为 HTTP 409。
/// </summary>
public sealed class ConflictException : BaseAppException
{
    /// <summary>
    /// 使用指定客户端消息创建冲突异常。
    /// </summary>
    /// <param name="message">面向客户端的冲突消息。</param>
    private ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "Resource Conflict", message)
    {
    }

    /// <summary>
    /// 从业务错误码及可选覆盖消息创建冲突异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">可选的客户端消息覆盖值。</param>
    /// <returns>配置完成的冲突异常。</returns>
    public static ConflictException Create(ErrorCodes errorCode, string? message = null)
    {
        return new ConflictException(message ?? errorCode.GetMessage());
    }
}
