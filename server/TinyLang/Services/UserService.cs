using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现用户资料、封禁状态和角色管理流程。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="userSessionService">用户会话失效服务。</param>
public sealed class UserService(
    IApplicationDbContext db,
    IUserSessionService userSessionService) : IUserService
{
    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <inheritdoc />
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

    /// <summary>
    /// 查找尚未软删除的用户。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="cancellationToken">用于取消查询的令牌。</param>
    /// <returns>已跟踪的用户实体。</returns>
    /// <exception cref="NotFoundException">用户不存在或已软删除。</exception>
    private async Task<User> FindUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        return await db.Users.SingleOrDefaultAsync(
            x => x.Id == userId && !x.IsDeleted,
            cancellationToken) ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
    }

    /// <summary>
    /// 将用户实体投影为资料响应。
    /// </summary>
    /// <param name="user">用户实体。</param>
    /// <returns>用户资料响应。</returns>
    private static UserProfileResponse ToProfileResponse(User user)
        => new(user.Id, user.Email, user.Role, user.Nickname, user.AvatarUrl, user.Bio, user.IsBanned);

    /// <summary>
    /// 将空白可选文本转换为 <see langword="null"/>，否则去除两端空白。
    /// </summary>
    /// <param name="value">待规范化文本。</param>
    /// <returns>规范化后的可选文本。</returns>
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
