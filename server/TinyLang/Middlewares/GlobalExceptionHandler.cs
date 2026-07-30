using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TinyLang.Exceptions;

namespace TinyLang.Middlewares;

/// <summary>
/// 将业务异常和未处理异常转换为统一 Problem Details 响应并记录日志。
/// </summary>
/// <param name="logger">异常日志记录器。</param>
/// <param name="problemDetailsService">Problem Details 响应写入服务。</param>
public sealed class GlobalExceptionHandler(
    ILogger<GlobalExceptionHandler> logger, IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        var statusCode = StatusCodes.Status500InternalServerError;
        var title = "Internal Server Error";
        var message = "Internal sever error, please try again later.";
        var errorCode = ErrorCodes.UnexpectedError;

        var problemDetails = new ProblemDetails();
        if (exception is BaseAppException appException)
        {
            statusCode = appException.StatusCode;
            title = appException.Title;
            message = appException.ErrorMessages;
            errorCode = appException.ErrorCode;

            if (appException is RequestValidationException validationException)
            {
                problemDetails.Extensions["errors"] = validationException.Errors;
            }

            logger.LogWarning(
                "Business rule violation {ErrorCode}: {Message}.",
                appException.ErrorCode,
                appException.Message);
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
        problemDetails.Extensions["errorCode"] = errorCode.ToString();

        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            Exception = exception,
            ProblemDetails = problemDetails
        });
    }
}
