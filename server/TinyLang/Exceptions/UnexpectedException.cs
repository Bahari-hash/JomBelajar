using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class UnexpectedException : BaseAppException
{
    private UnexpectedException(string message)
        : base(StatusCodes.Status500InternalServerError, "Internal Server Error", message)
    {
    }

    public static UnexpectedException Create(ErrorCodes errorCode, string? message = null)
    {
        return new UnexpectedException(message ?? errorCode.GetMessage());
    }
}
