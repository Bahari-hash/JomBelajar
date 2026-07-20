using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

public sealed class AccountSecurityService(
    IApplicationDbContext db,
    ISecretHasher secretHasher,
    IAuthService authService) : IAccountSecurityService
{
    public async Task ResetPasswordAsync(
        Guid userId,
        ResetPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        if (!await authService.VerifyCodeAsync(
                user.Email,
                VerificationCodePurpose.ResetPassword,
                request.VerificationCode,
                cancellationToken))
        {
            throw UnauthorizedException.Create(ErrorCodes.VerificationCodeInvalid);
        }

        user.PasswordHash = secretHasher.Hash(request.NewPassword);
        await InvalidateSessionsAsync(user, cancellationToken);
    }

    public async Task<ChangeEmailResponse> ChangeEmailAsync(
        Guid userId,
        ChangeEmailRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        var newEmail = NormalizeEmail(request.NewEmail);
        if (newEmail == user.Email)
        {
            throw new RequestValidationException(ErrorCodes.EmailUnchanged);
        }
        if (await db.Users.AnyAsync(x => x.Email == newEmail && !x.IsDeleted, cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }
        if (!await authService.VerifyCodeAsync(
                newEmail,
                VerificationCodePurpose.ChangeEmail,
                request.VerificationCode,
                cancellationToken))
        {
            throw UnauthorizedException.Create(ErrorCodes.VerificationCodeInvalid);
        }

        user.Email = newEmail;
        await InvalidateSessionsAsync(user, cancellationToken);
        return new ChangeEmailResponse(user.Id, user.Email);
    }

    public async Task DeleteAccountAsync(
        Guid userId,
        DeleteAccountRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindActiveUserAsync(userId, cancellationToken);
        if (!await authService.VerifyCodeAsync(
                user.Email,
                VerificationCodePurpose.DeleteAccount,
                request.VerificationCode,
                cancellationToken))
        {
            throw UnauthorizedException.Create(ErrorCodes.VerificationCodeInvalid);
        }

        var deletedAt = DateTimeOffset.UtcNow;
        var identifier = user.Id.ToString("N");
        user.IsDeleted = true;
        user.DeletedAt = deletedAt;
        user.Username = $"deleted-{identifier}";
        user.Email = $"deleted-{identifier}@deleted.invalid";
        await InvalidateSessionsAsync(user, cancellationToken);
    }

    private async Task<User> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    private async Task InvalidateSessionsAsync(User user, CancellationToken cancellationToken)
    {
        user.TokenVersion++;
        var now = DateTimeOffset.UtcNow;
        var refreshTokens = await db.RefreshTokens
            .Where(x => x.UserId == user.Id && !x.IsRevoked)
            .ToListAsync(cancellationToken);
        foreach (var refreshToken in refreshTokens)
        {
            refreshToken.IsRevoked = true;
            refreshToken.RevokedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
