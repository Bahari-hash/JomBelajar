using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class UnauthorizedException : BaseAppException
{
    private UnauthorizedException(string message)
        : base(StatusCodes.Status401Unauthorized, "User Unauthorized", message)
    {
    }

    public static UnauthorizedException Create(ErrorCodes errorCode, string? message = null)
    {
        return new UnauthorizedException(message ?? errorCode.GetMessage());
    }
}
