using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class ValidationException : BaseAppException
{
    private ValidationException(string message)
        : base(StatusCodes.Status400BadRequest, "Validation Failed", message)
    {
    }

    public ValidationException Create(ErrorCodes errorCode, string? message = null)
    {
        return new ValidationException(message ?? errorCode.GetMessage());
    }
}
