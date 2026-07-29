using FluentValidation;
using TinyLang.Exceptions;

namespace TinyLang.Services;

/// <summary>
/// 让业务服务在脱离 HTTP filter 调用时复用请求验证规则。
/// </summary>
internal static class ServiceRequestValidator
{
    /// <summary>
    /// 执行同步请求验证并将失败转换为统一业务验证异常。
    /// </summary>
    /// <typeparam name="T">请求类型。</typeparam>
    /// <param name="request">待验证请求。</param>
    /// <param name="validator">该请求的 FluentValidation validator。</param>
    public static void Validate<T>(T request, IValidator<T> validator)
    {
        var result = validator.Validate(request);
        if (result.IsValid)
        {
            return;
        }

        var errors = result.Errors
            .GroupBy(value => value.PropertyName)
            .ToDictionary(
                group => group.Key,
                group => group.Select(value => value.ErrorMessage).ToArray());
        var firstCode = result.Errors.Select(value => value.ErrorCode)
            .Select(value => Enum.TryParse<ErrorCodes>(value, out var parsed)
                ? parsed
                : ErrorCodes.RequestValidationFailed)
            .First();
        throw new RequestValidationException(firstCode, errors);
    }
}
