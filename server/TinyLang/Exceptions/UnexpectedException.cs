using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示已转换为稳定客户端消息的系统或外部服务失败，并映射为 HTTP 500。
/// </summary>
public sealed class UnexpectedException : BaseAppException
{
    /// <summary>
    /// 使用指定客户端消息创建系统异常。
    /// </summary>
    /// <param name="message">面向客户端的系统错误消息。</param>
    private UnexpectedException(string message)
        : base(StatusCodes.Status500InternalServerError, "Internal Server Error", message)
    {
    }

    /// <summary>
    /// 从业务错误码及可选覆盖消息创建系统异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">可选的客户端消息覆盖值。</param>
    /// <returns>配置完成的系统异常。</returns>
    public static UnexpectedException Create(ErrorCodes errorCode, string? message = null)
    {
        return new UnexpectedException(message ?? errorCode.GetMessage());
    }
}
