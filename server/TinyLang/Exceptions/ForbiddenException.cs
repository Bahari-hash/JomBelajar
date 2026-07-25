using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示已认证用户无权执行操作，并映射为 HTTP 403。
/// </summary>
public sealed class ForbiddenException : BaseAppException
{
    /// <summary>
    /// 使用指定客户端消息创建禁止访问异常。
    /// </summary>
    /// <param name="message">面向客户端的权限消息。</param>
    private ForbiddenException(string message)
        : base(StatusCodes.Status403Forbidden, "Permission Denied", message)
    {
    }

    /// <summary>
    /// 从业务错误码及可选覆盖消息创建禁止访问异常。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="message">可选的客户端消息覆盖值。</param>
    /// <returns>配置完成的禁止访问异常。</returns>
    public static ForbiddenException Create(ErrorCodes errorCode, string? message = null)
    {
        return new ForbiddenException(message ?? errorCode.GetMessage());
    }
}
