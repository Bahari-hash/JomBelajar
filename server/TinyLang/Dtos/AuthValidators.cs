using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 校验注册验证码申请中的邮箱。
/// </summary>
public sealed class RegisterTokenRequestValidator : AbstractValidator<RegisterTokenRequest>
{
    /// <summary>
    /// 初始化注册验证码申请的校验规则。
    /// </summary>
    public RegisterTokenRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);
    }
}

/// <summary>
/// 校验账户注册请求中的邮箱、密码和验证码。
/// </summary>
public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    /// <summary>
    /// 初始化账户注册请求的校验规则。
    /// </summary>
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrKey(ErrorCodes.PasswordRequired)
            .MinimumLength(8).WithErrKey(ErrorCodes.PasswordLengthMinimum)
            .MaximumLength(50).WithErrKey(ErrorCodes.PasswordLengthLimit);

        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithErrKey(ErrorCodes.VerificationCodeRequired)
            .Length(6).WithErrKey(ErrorCodes.VerificationCodeLengthLimit)
            .Matches("^[0-9]+$").WithErrKey(ErrorCodes.VerificationCodeFormatInvalid);
    }
}

/// <summary>
/// 校验邮箱密码登录请求。
/// </summary>
public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>
    /// 初始化登录请求的校验规则。
    /// </summary>
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrKey(ErrorCodes.PasswordRequired)
            .MinimumLength(8).WithErrKey(ErrorCodes.PasswordLengthMinimum)
            .MaximumLength(50).WithErrKey(ErrorCodes.PasswordLengthLimit);
    }
}