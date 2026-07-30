using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示请求超过允许频率，并映射为 HTTP 429。
/// </summary>
public sealed class TooManyRequestsException : BaseAppException
{
    /// <summary>
    /// 使用指定客户端消息创建限流异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">面向客户端的限流消息。</param>
    private TooManyRequestsException(ErrorCodes errorCode, string message)
        : base(errorCode, StatusCodes.Status429TooManyRequests, "Too Many Requests", message)
    {
    }

    /// <summary>
    /// 从业务错误码及可选覆盖消息创建限流异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">可选的客户端消息覆盖值。</param>
    /// <returns>配置完成的限流异常。</returns>
    public static TooManyRequestsException Create(ErrorCodes errorCode, string? message = null)
    {
        return new TooManyRequestsException(errorCode, message ?? errorCode.GetMessage());
    }
}
