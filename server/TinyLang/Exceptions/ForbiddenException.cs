using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class ForbiddenException : BaseAppException
{
    private ForbiddenException(string message)
        : base(StatusCodes.Status403Forbidden, "Permission Denied", message)
    {
    }

    public ForbiddenException Cretae(ErrorCodes errorCode, string? message = null)
    {
        return new ForbiddenException(message ?? errorCode.GetMessage());
    }
}
