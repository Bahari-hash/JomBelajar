using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示身份、凭据或令牌无效，并映射为 HTTP 401。
/// </summary>
public sealed class UnauthorizedException : BaseAppException
{
    /// <summary>
    /// 使用指定客户端消息创建未授权异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">面向客户端的认证消息。</param>
    private UnauthorizedException(ErrorCodes errorCode, string message)
        : base(errorCode, StatusCodes.Status401Unauthorized, "User Unauthorized", message)
    {
    }

    /// <summary>
    /// 从业务错误码及可选覆盖消息创建未授权异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">可选的客户端消息覆盖值。</param>
    /// <returns>配置完成的未授权异常。</returns>
    public static UnauthorizedException Create(ErrorCodes errorCode, string? message = null)
    {
        return new UnauthorizedException(errorCode, message ?? errorCode.GetMessage());
    }
}
