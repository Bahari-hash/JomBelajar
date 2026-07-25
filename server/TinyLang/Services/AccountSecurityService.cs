using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 编排验证码、凭据更新、账户软删除和会话失效操作。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="secretHasher">密码散列服务。</param>
/// <param name="authService">验证码验证服务。</param>
/// <param name="userSessionService">用户会话失效服务。</param>
/// <param name="databaseExceptionClassifier">数据库约束异常分类器。</param>
public sealed class AccountSecurityService(
    IApplicationDbContext db,
    ISecretHasher secretHasher,
    IAuthService authService,
    IUserSessionService userSessionService,
    IDatabaseExceptionClassifier databaseExceptionClassifier) : IAccountSecurityService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>
    /// 查找尚未软删除的用户。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>已跟踪的有效用户。</returns>
    /// <exception cref="NotFoundException">用户不存在或已软删除。</exception>
    private async Task<User> FindActiveUserAsync(Guid userId, CancellationToken cancellationToken)
        => await db.Users.SingleOrDefaultAsync(x => x.Id == userId && !x.IsDeleted, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    /// <summary>
    /// 去除邮箱两端空白并转换为小写形式。
    /// </summary>
    /// <param name="email">待规范化邮箱。</param>
    /// <returns>规范化后的邮箱。</returns>
    private static string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();
}
