using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

/// <summary>
/// 校验换绑邮箱验证码发送请求中的新邮箱。
/// </summary>
public sealed class SendChangeEmailTokenRequestValidator : AbstractValidator<SendChangeEmailTokenRequest>
{
    /// <summary>
    /// 初始化换绑邮箱验证码发送请求的校验规则。
    /// </summary>
    public SendChangeEmailTokenRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);
    }
}

/// <summary>
/// 校验密码重置请求中的新密码和验证码。
/// </summary>
public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    /// <summary>
    /// 初始化密码重置请求的校验规则。
    /// </summary>
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword)
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
/// 校验邮箱换绑请求中的新邮箱和验证码。
/// </summary>
public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
    /// <summary>
    /// 初始化邮箱换绑请求的校验规则。
    /// </summary>
    public ChangeEmailRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);

        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithErrKey(ErrorCodes.VerificationCodeRequired)
            .Length(6).WithErrKey(ErrorCodes.VerificationCodeLengthLimit)
            .Matches("^[0-9]+$").WithErrKey(ErrorCodes.VerificationCodeFormatInvalid);
    }
}

/// <summary>
/// 校验账户删除请求中的验证码。
/// </summary>
public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    /// <summary>
    /// 初始化账户删除请求的校验规则。
    /// </summary>
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithErrKey(ErrorCodes.VerificationCodeRequired)
            .Length(6).WithErrKey(ErrorCodes.VerificationCodeLengthLimit)
            .Matches("^[0-9]+$").WithErrKey(ErrorCodes.VerificationCodeFormatInvalid);
    }
}
