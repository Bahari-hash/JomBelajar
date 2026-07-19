using Microsoft.AspNetCore.Http;

namespace TinyLang.Exceptions;

public sealed class RequestValidationException : BaseAppException
{
    public IDictionary<string, string[]>? Errors { get; }

    public RequestValidationException(
        ErrorCodes errorCode, IDictionary<string, string[]>? errors = null, string? customMessage = null)
        : base(StatusCodes.Status400BadRequest, "Request Validation Failed", customMessage ?? errorCode.GetMessage())
    {
        Errors = errors;
    }
}
