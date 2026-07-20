using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

public sealed class RegisterTokenRequestValidator : AbstractValidator<RegisterTokenRequest>
{
    public RegisterTokenRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);
    }
}

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
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

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);

        RuleFor(x => x.Password)
            .NotEmpty().WithErrKey(ErrorCodes.PasswordRequired)
            .MaximumLength(50).WithErrKey(ErrorCodes.PasswordLengthLimit);
    }
}

public sealed class RefreshTokenRequestValidator : AbstractValidator<RefreshTokenRequest>
{
    public RefreshTokenRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithErrKey(ErrorCodes.RefreshTokenInvalid);
    }
}

public sealed class LogoutRequestValidator : AbstractValidator<LogoutRequest>
{
    public LogoutRequestValidator()
    {
        RuleFor(x => x.RefreshToken)
            .NotEmpty().WithErrKey(ErrorCodes.RefreshTokenInvalid);
    }
}
