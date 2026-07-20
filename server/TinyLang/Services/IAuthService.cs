using TinyLang.Dtos;
using TinyLang.Enums;

namespace TinyLang.Services;

public interface IAuthService
{
    Task SendRegisterTokenAsync(string email, CancellationToken cancellationToken = default);
    Task<UserResponse> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken = default);
    Task<AuthTokenResponse> LoginAsync(LoginRequest request, string? clientIp, string? deviceInfo, CancellationToken cancellationToken = default);
    Task<AuthTokenResponse> RefreshAsync(RefreshTokenRequest request, string? clientIp, string? deviceInfo, CancellationToken cancellationToken = default);
    Task LogoutAsync(Guid userId, string? accessToken, string? refreshToken, CancellationToken cancellationToken = default);
    Task RevokeUserAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SendChangeEmailTokenAsync(Guid userId, string newEmail, CancellationToken cancellationToken = default);
    Task SendResetPasswordTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task SendDeleteAccountTokenAsync(Guid userId, CancellationToken cancellationToken = default);
    Task<bool> VerifyCodeAsync(string email, VerificationCodePurpose purpose, string code, CancellationToken cancellationToken = default);
}
