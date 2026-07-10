using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class ConflictException : BaseAppException
{
    private ConflictException(string message)
        : base(StatusCodes.Status409Conflict, "Resource Conflict", message)
    {
    }

    public static ConflictException Create(ErrorCodes errorCode, string? message = null)
    {
        return new ConflictException(message ?? errorCode.GetMessage());
    }
}
