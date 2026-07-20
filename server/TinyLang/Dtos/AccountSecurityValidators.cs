using FluentValidation;
using TinyLang.Exceptions;
using TinyLang.Extensions;

namespace TinyLang.Dtos;

public sealed class SendChangeEmailTokenRequestValidator : AbstractValidator<SendChangeEmailTokenRequest>
{
    public SendChangeEmailTokenRequestValidator()
    {
        RuleFor(x => x.NewEmail)
            .NotEmpty().WithErrKey(ErrorCodes.EmailRequired)
            .EmailAddress().WithErrKey(ErrorCodes.EmailFormatInvalid)
            .MaximumLength(100).WithErrKey(ErrorCodes.EmailLengthLimit);
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
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

public sealed class ChangeEmailRequestValidator : AbstractValidator<ChangeEmailRequest>
{
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

public sealed class DeleteAccountRequestValidator : AbstractValidator<DeleteAccountRequest>
{
    public DeleteAccountRequestValidator()
    {
        RuleFor(x => x.VerificationCode)
            .NotEmpty().WithErrKey(ErrorCodes.VerificationCodeRequired)
            .Length(6).WithErrKey(ErrorCodes.VerificationCodeLengthLimit)
            .Matches("^[0-9]+$").WithErrKey(ErrorCodes.VerificationCodeFormatInvalid);
    }
}
