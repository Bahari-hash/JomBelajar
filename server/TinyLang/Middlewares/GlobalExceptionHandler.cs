using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TinyLang.Exceptions;

namespace TinyLang.Middlewares;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var message = "Internal sever error, please try again later.";

        var problemDetails = new ProblemDetails();
        if (exception is BaseAppException appException)
        {
            statusCode = appException.StatusCode;
            title = appException.Title;
            message = appException.ErrorMessages;

            if (appException is RequestValidationException validationException)
            {
                problemDetails.Extensions["errors"] = validationException.Errors;
            }

            logger.LogWarning("Business rule violation: {Message}.", appException.Message);
        }
        else
        {
            logger.LogError(exception, "Unhandled system exception: {Message}.", exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;

        problemDetails.Type = $"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{statusCode}";
        problemDetails.Status = statusCode;
        problemDetails.Title = title;
        problemDetails.Detail = message;
        problemDetails.Instance = httpContext.Request.Path;

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
