namespace TinyLang.Exceptions;

/// <summary>
/// 为可预期业务失败携带稳定 HTTP 状态、标题和客户端消息。
/// </summary>
/// <param name="statusCode">映射到 HTTP 响应的状态码。</param>
/// <param name="title">Problem Details 标题。</param>
/// <param name="messages">可返回客户端的错误消息。</param>
public abstract class BaseAppException(int statusCode, string title, string messages) : Exception(messages)
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;

    public string ErrorMessages { get; } = messages;
}
