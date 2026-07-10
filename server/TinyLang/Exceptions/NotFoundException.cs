using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class NotFoundException : BaseAppException
{
    private NotFoundException(string message)
        : base(StatusCodes.Status404NotFound, "Resource Not Found", message)
    {
    }

    public static NotFoundException Create(ErrorCodes errorCode, string? message = null)
    {
        return new NotFoundException(message ?? errorCode.GetMessage());
    }
}
