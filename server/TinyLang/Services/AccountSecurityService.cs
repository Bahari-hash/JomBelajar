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
    IAuthService authService,
    IUserSessionService userSessionService,
    IDatabaseExceptionClassifier databaseExceptionClassifier) : IAccountSecurityService
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
        await userSessionService.InvalidateAllAsync(user, cancellationToken);
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
        try
        {
            await userSessionService.InvalidateAllAsync(user, cancellationToken);
        }
        catch (DbUpdateException exception) when (databaseExceptionClassifier.IsUniqueConstraintViolation(
            exception,
            "IX_users_Email"))
        {
            throw ConflictException.Create(ErrorCodes.EmailAlreadyExists);
        }
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
        await userSessionService.InvalidateAllAsync(user, cancellationToken);
    }

    private async Task<User> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
