using System.Collections.Concurrent;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using TinyLang.Exceptions;

namespace TinyLang.Filters;

/// <summary>
/// 对 endpoint 的复杂参数运行已注册 FluentValidation validator，并统一抛出字段错误。
/// </summary>
public sealed class ValidationEndpointFilter : IEndpointFilter
{
    private static readonly ConcurrentDictionary<Type, Type> ValidatorTypeCache = new();

    /// <inheritdoc />
    public async ValueTask<object?> InvokeAsync(
        EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        foreach (var argument in context.Arguments)
        {
            if (argument is null)
            {
                continue;
            }

            var argumentType = argument.GetType();
            if (argumentType.IsValueType || argumentType == typeof(string))
            {
                continue;
            }

            var validatorType = ValidatorTypeCache.GetOrAdd(
                argumentType,
                type => typeof(IValidator<>).MakeGenericType(argumentType));

            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(validationContext);

            if (!result.IsValid)
            {
                var errorDictionary = result.Errors
                    .GroupBy(e => e.PropertyName)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(x => x.ErrorMessage).ToArray());

                throw new RequestValidationException(
                    ErrorCodes.RequestValidationFailed,
                    errorDictionary);
            }
        }

        return await next(context);
    }
}
