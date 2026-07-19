using TinyLang.Enums;

namespace TinyLang.Interfaces;

public interface IVerificationCodeSender
{
    Task SendCodeAsync(string email, VerificationCodePurpose purpose, CancellationToken cancellationToken = default);

    Task<bool> VerifyCodeAsync(string email, VerificationCodePurpose purpose, string code, CancellationToken cancellationToken = default);
}
