using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class TooManyRequestsException : BaseAppException
{
    private TooManyRequestsException(string message)
        : base(StatusCodes.Status429TooManyRequests, "Too Many Requests", message)
    {
    }

    public TooManyRequestsException Create(ErrorCodes errorCode, string? message = null)
    {
        return new TooManyRequestsException(message ?? errorCode.GetMessage());
    }
}
