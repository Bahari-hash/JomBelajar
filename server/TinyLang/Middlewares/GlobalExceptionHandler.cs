using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TinyLang.Exceptions;

namespace TinyLang.Middlewares;

public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger = logger;
    private readonly IProblemDetailsService _problemDetailsService = problemDetailsService;

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var message = "Internal sever error, please try again later.";

        if (exception is BaseAppException appException)
        {
            statusCode = appException.StatusCode;
            title = appException.Title;
            message = appException.ErrorMessages;

            _logger.LogWarning("Business rule violation: {Message}", appException.Message);
        }
        else
        {
            _logger.LogError(exception, "Unhandled system exception: {Message}", exception.Message);
        }

        httpContext.Response.StatusCode = statusCode;
        var problemDetails = new ProblemDetails
        {
            Type = $"https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/{statusCode}",
            Status = statusCode,
            Title = title,
            Detail = message,
            Instance = httpContext.Request.Path
        };

        return await _problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
