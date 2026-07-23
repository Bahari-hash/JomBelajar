using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

public sealed class UserService(
    IApplicationDbContext db,
    IUserSessionService userSessionService) : IUserService
{
    public async Task<UserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(userId, cancellationToken);
        user.Nickname = NormalizeOptional(request.Nickname);
        user.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        user.Bio = NormalizeOptional(request.Bio);

        await db.SaveChangesAsync(cancellationToken);
        return ToProfileResponse(user);
    }

    public async Task BanAsync(
        Guid operatorId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        if (operatorId == targetUserId)
        {
            throw ForbiddenException.Create(ErrorCodes.CannotBanSelf);
        }

        var user = await FindUserAsync(targetUserId, cancellationToken);
        if (!user.IsBanned)
        {
            user.IsBanned = true;
            user.BannedAt = DateTimeOffset.UtcNow;
            await userSessionService.InvalidateAllAsync(user, cancellationToken);
        }
    }

    public async Task UnbanAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(targetUserId, cancellationToken);
        if (user.IsBanned)
        {
            user.IsBanned = false;
            user.BannedAt = null;
            user.BannedReason = null;
            user.TokenVersion++;
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<UserRoleResponse> UpdateRoleAsync(
        Guid targetUserId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindUserAsync(targetUserId, cancellationToken);
        if (!Enum.TryParse<UserRole>(request.Role, ignoreCase: true, out var role) ||
            !Enum.IsDefined(role))
        {
            throw new RequestValidationException(ErrorCodes.RoleInvalid);
        }

        user.Role = role;
        user.TokenVersion++;
        await db.SaveChangesAsync(cancellationToken);
        return new UserRoleResponse(user.Id, user.Role);
    }

    private async Task<User> FindUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId && !x.IsDeleted,
            cancellationToken) ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
    }

    private static UserProfileResponse ToProfileResponse(User user)
        => new(user.Id, user.Email, user.Role, user.Nickname, user.AvatarUrl, user.Bio, user.IsBanned);

    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
