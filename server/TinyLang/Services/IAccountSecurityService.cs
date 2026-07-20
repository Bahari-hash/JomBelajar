using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IAccountSecurityService
{
    Task ResetPasswordAsync(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default);

    Task<ChangeEmailResponse> ChangeEmailAsync(
        Guid userId,
        ChangeEmailRequest request,
        CancellationToken cancellationToken = default);

    Task DeleteAccountAsync(
        Guid userId,
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default);
}
