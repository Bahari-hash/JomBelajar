using FluentValidation;
using TinyLang.Exceptions;

namespace TinyLang.Extensions;

/// <summary>
/// 提供将 TinyLang 错误码附加到 FluentValidation 规则的扩展方法。
/// </summary>
public static class FluentValidationExtension
{
    /// <summary>
    /// 将业务错误码名称和描述设置为验证规则的错误代码与消息。
    /// </summary>
    /// <typeparam name="T">被验证对象类型。</typeparam>
    /// <typeparam name="TProperty">被验证属性类型。</typeparam>
    /// <param name="ruleBuilder">当前 FluentValidation 规则构建器。</param>
    /// <param name="errorCode">要附加的 TinyLang 错误码。</param>
    /// <returns>可继续配置的同一规则构建器。</returns>
    public static IRuleBuilderOptions<T, TProperty> WithErrKey<T, TProperty>(
        this IRuleBuilderOptions<T, TProperty> ruleBuilder,
        ErrorCodes errorCode)
    {
        return ruleBuilder
            .WithErrorCode(errorCode.ToString())
            .WithMessage(errorCode.GetMessage());
    }
}
