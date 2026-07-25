using TinyLang.Enums;

namespace TinyLang.Interfaces;

public interface IVerificationCodeStore
{
    Task SaveAsync(
        string email,
        VerificationCodePurpose purpose,
        string code,
        CancellationToken cancellationToken = default);

    Task<string?> GetAsync(
        string email,
        VerificationCodePurpose purpose,
        CancellationToken cancellationToken = default);

    Task<bool> TryConsumeAsync(
        string email,
        VerificationCodePurpose purpose,
        string expectedValue,
        CancellationToken cancellationToken = default);
}
