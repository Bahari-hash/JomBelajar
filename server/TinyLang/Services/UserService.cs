using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现隔离的用户资料查询、管理员分页、封禁状态和角色管理流程。
/// </summary>
/// <param name="db">应用数据库上下文。</param>
/// <param name="userSessionService">用户会话失效服务。</param>
/// <param name="timeProvider">业务 UTC 时间源。</param>
/// <param name="logger">结构化日志记录器。</param>
public sealed class UserService(
    IApplicationDbContext db,
    IUserSessionService userSessionService,
    TimeProvider timeProvider,
    ILogger<UserService> logger) : IUserService
{
    /// <inheritdoc />
    public async Task<CurrentUserProfileResponse> GetCurrentProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await db.Users.AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted && !user.IsBanned)
            .Select(user => new CurrentUserProfileResponse(
                user.Id,
                user.Email,
                user.Role,
                user.Nickname,
                user.AvatarUrl,
                user.Bio,
                user.CreatedAt))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    /// <inheritdoc />
    public async Task<PublicUserProfileResponse> GetPublicProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
        => await db.Users.AsNoTracking()
            .Where(user => user.Id == userId && !user.IsDeleted && !user.IsBanned)
            .Select(user => new PublicUserProfileResponse(
                user.Id,
                user.Nickname,
                user.AvatarUrl,
                user.Bio))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    /// <inheritdoc />
    public async Task<PagedResponse<AdminUserListItemResponse>> GetAdminListAsync(
        AdminUserListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = ApplyAdminFilters(db.Users.AsNoTracking(), request);
        var totalCount = await query.CountAsync(cancellationToken);
        var now = timeProvider.GetUtcNow();
        var items = await query
            .OrderByDescending(user => user.CreatedAt)
            .ThenByDescending(user => user.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(user => new AdminUserListItemResponse(
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.Nickname,
                user.AvatarUrl,
                user.IsBanned,
                user.BannedAt,
                user.BannedReason,
                user.IsDeleted,
                user.DeletedAt,
                user.CreatedAt,
                user.UpdatedAt,
                user.RefreshTokens.Max(token => token.LoginAt),
                !user.IsDeleted && !user.IsBanned
                    ? user.RefreshTokens.Count(token =>
                        !token.IsRevoked &&
                        token.ExpiresAt > now &&
                        token.TokenVersion == user.TokenVersion)
                    : 0))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminUserListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            (totalCount + request.PageSize - 1) / request.PageSize);
    }

    /// <inheritdoc />
    public async Task<AdminUserDetailResponse> GetAdminByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        return await db.Users.AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new AdminUserDetailResponse(
                user.Id,
                user.Username,
                user.Email,
                user.Role,
                user.Nickname,
                user.AvatarUrl,
                user.Bio,
                user.IsBanned,
                user.BannedAt,
                user.BannedReason,
                user.IsDeleted,
                user.DeletedAt,
                user.CreatedAt,
                user.UpdatedAt,
                user.RefreshTokens.Max(token => token.LoginAt),
                !user.IsDeleted && !user.IsBanned
                    ? user.RefreshTokens.Count(token =>
                        !token.IsRevoked &&
                        token.ExpiresAt > now &&
                        token.TokenVersion == user.TokenVersion)
                    : 0))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);
    }

    /// <inheritdoc />
    public async Task<CurrentUserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedUserAsync(userId, cancellationToken);
        if (user.IsBanned)
        {
            throw NotFoundException.Create(ErrorCodes.UserNotFound);
        }

        user.Nickname = NormalizeOptional(request.Nickname);
        user.AvatarUrl = NormalizeOptional(request.AvatarUrl);
        user.Bio = NormalizeOptional(request.Bio);

        await db.SaveChangesAsync(cancellationToken);
        return ToCurrentProfileResponse(user);
    }

    /// <inheritdoc />
    public async Task BanAsync(
        Guid operatorId,
        Guid targetUserId,
        BanUserRequest request,
        CancellationToken cancellationToken = default)
    {
        if (operatorId == targetUserId)
        {
            throw ForbiddenException.Create(ErrorCodes.CannotBanSelf);
        }

        var reason = NormalizeBanReason(request.Reason);
        var user = await FindManagedUserAsync(targetUserId, cancellationToken);
        if (user.IsBanned)
        {
            if (string.Equals(user.BannedReason, reason, StringComparison.Ordinal))
            {
                return;
            }

            user.BannedReason = reason;
            await db.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Updated ban reason for user {TargetUserId} by administrator {OperatorId}; reason length {ReasonLength}",
                targetUserId,
                operatorId,
                reason.Length);
            return;
        }

        user.IsBanned = true;
        user.BannedAt = timeProvider.GetUtcNow();
        user.BannedReason = reason;
        await userSessionService.InvalidateAllAsync(user, cancellationToken);
        logger.LogInformation(
            "Banned user {TargetUserId} by administrator {OperatorId}; reason length {ReasonLength}",
            targetUserId,
            operatorId,
            reason.Length);
    }

    /// <inheritdoc />
    public async Task UnbanAsync(
        Guid operatorId,
        Guid targetUserId,
        CancellationToken cancellationToken = default)
    {
        var user = await FindManagedUserAsync(targetUserId, cancellationToken);
        if (!user.IsBanned)
        {
            return;
        }

        user.IsBanned = false;
        user.BannedAt = null;
        user.BannedReason = null;
        await db.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Unbanned user {TargetUserId} by administrator {OperatorId}",
            targetUserId,
            operatorId);
    }

    /// <inheritdoc />
    public async Task<UserRoleResponse> UpdateRoleAsync(
        Guid operatorId,
        Guid targetUserId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        if (operatorId == targetUserId)
        {
            throw ForbiddenException.Create(ErrorCodes.CannotChangeOwnRole);
        }
        if (!UserRoleParser.TryParse(request.Role, out var role))
        {
            throw new RequestValidationException(ErrorCodes.RoleInvalid);
        }

        var user = await FindManagedUserAsync(targetUserId, cancellationToken);
        if (user.Role == role)
        {
            return new UserRoleResponse(user.Id, user.Role);
        }

        var oldRole = user.Role;
        user.Role = role;
        await userSessionService.InvalidateAllAsync(user, cancellationToken);
        logger.LogInformation(
            "Changed user {TargetUserId} role from {OldRole} to {NewRole} by administrator {OperatorId}",
            targetUserId,
            oldRole,
            role,
            operatorId);
        return new UserRoleResponse(user.Id, user.Role);
    }

    /// <summary>
    /// 应用管理员用户列表的删除状态、角色、账户状态和关键词筛选。
    /// </summary>
    private static IQueryable<User> ApplyAdminFilters(
        IQueryable<User> query,
        AdminUserListRequest request)
    {
        query = request.Status switch
        {
            AdminUserStatus.Active => query.Where(user =>
                !user.IsDeleted && !user.IsBanned),
            AdminUserStatus.Banned => query.Where(user =>
                !user.IsDeleted && user.IsBanned),
            AdminUserStatus.Deleted => query.Where(user => user.IsDeleted),
            _ => query.Where(user => !user.IsDeleted)
        };

        if (request.Role is { } roleName)
        {
            if (!UserRoleParser.TryParse(roleName, out var role))
            {
                throw new RequestValidationException(ErrorCodes.RoleInvalid);
            }

            query = query.Where(user => user.Role == role);
        }

        var keyword = NormalizeOptional(request.Keyword);
        if (keyword is null)
        {
            return query;
        }

        var normalizedKeyword = keyword.ToUpperInvariant();
        if (Guid.TryParse(keyword, out var userId))
        {
            return query.Where(user =>
                user.Id == userId ||
                user.Email.ToUpper().Contains(normalizedKeyword) ||
                user.Username.ToUpper().Contains(normalizedKeyword) ||
                (user.Nickname != null &&
                    user.Nickname.ToUpper().Contains(normalizedKeyword)));
        }

        return query.Where(user =>
            user.Email.ToUpper().Contains(normalizedKeyword) ||
            user.Username.ToUpper().Contains(normalizedKeyword) ||
            (user.Nickname != null &&
                user.Nickname.ToUpper().Contains(normalizedKeyword)));
    }

    /// <summary>
    /// 查找尚未软删除的 tracked 用户。
    /// </summary>
    private async Task<User> FindManagedUserAsync(
        Guid userId,
        CancellationToken cancellationToken)
        => await db.Users.SingleOrDefaultAsync(
            user => user.Id == userId && !user.IsDeleted,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.UserNotFound);

    /// <summary>
    /// 将 tracked 用户映射为当前用户私有资料响应。
    /// </summary>
    private static CurrentUserProfileResponse ToCurrentProfileResponse(User user)
        => new(
            user.Id,
            user.Email,
            user.Role,
            user.Nickname,
            user.AvatarUrl,
            user.Bio,
            user.CreatedAt);

    /// <summary>
    /// 规范化并防御性验证管理员提交的封禁原因。
    /// </summary>
    private static string NormalizeBanReason(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new RequestValidationException(ErrorCodes.BanUserReasonRequired);
        }

        var normalized = value.Trim();
        if (normalized.Length > 500)
        {
            throw new RequestValidationException(ErrorCodes.BanUserReasonLengthLimit);
        }
        return normalized;
    }

    /// <summary>
    /// 将空白可选文本转换为 null，否则去除两端空白。
    /// </summary>
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
