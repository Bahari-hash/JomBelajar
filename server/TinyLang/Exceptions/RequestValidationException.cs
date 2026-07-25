using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

/// <summary>
/// 表示请求字段或跨字段业务校验失败，并映射为 HTTP 400。
/// </summary>
public sealed class RequestValidationException : BaseAppException
{
    public IDictionary<string, string[]>? Errors { get; }

    /// <summary>
    /// 创建请求校验异常，并可附加按字段分组的错误集合。
    /// </summary>
    /// <param name="errorCode">业务错误码。</param>
    /// <param name="errors">可选的字段错误集合。</param>
    /// <param name="customMessage">可选的顶层客户端消息。</param>
    public RequestValidationException(
        ErrorCodes errorCode, IDictionary<string, string[]>? errors = null, string? customMessage = null)
        : base(StatusCodes.Status400BadRequest, "Request Validation Failed", customMessage ?? errorCode.GetMessage())
    {
        Errors = errors;
    }
}
