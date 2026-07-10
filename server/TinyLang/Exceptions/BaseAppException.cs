namespace TinyLang.Exceptions;

public abstract class BaseAppException(int statusCode, string title, string messages) : Exception
{
    public int StatusCode { get; } = statusCode;
    public string Title { get; } = title;

    public string ErrorMessages { get; } = messages;
}
