using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示请求资源不存在，并映射为 HTTP 404。
/// </summary>
public sealed class NotFoundException : BaseAppException
{
    /// <summary>
    /// 使用指定客户端消息创建资源不存在异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">面向客户端的不存在消息。</param>
    private NotFoundException(ErrorCodes errorCode, string message)
        : base(errorCode, StatusCodes.Status404NotFound, "Resource Not Found", message)
    {
    }

    /// <summary>
    /// 从业务错误码及可选覆盖消息创建资源不存在异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">可选的客户端消息覆盖值。</param>
    /// <returns>配置完成的资源不存在异常。</returns>
    public static NotFoundException Create(ErrorCodes errorCode, string? message = null)
    {
        return new NotFoundException(errorCode, message ?? errorCode.GetMessage());
    }
}
