using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 描述当前用户可修改的公开资料字段。
/// </summary>
public sealed record UpdateProfileRequest
{
    public string? Nickname { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Bio { get; init; }
}

/// <summary>
/// 描述用户每日自动背诵数量设置。
/// </summary>
public sealed record UpdateWordStudySettingsRequest
{
    public int DailyWordStudyCount { get; init; }
}

/// <summary>
/// 返回用户每日自动背诵数量设置。
/// </summary>
public sealed record WordStudySettingsResponse(int DailyWordStudyCount);

/// <summary>
/// 描述管理员修改用户角色的请求。
/// </summary>
public sealed record UpdateRoleRequest
{
    public required string Role { get; init; }
}

/// <summary>
/// 描述管理员封禁用户时必须持久化的原因。
/// </summary>
public sealed record BanUserRequest
{
    public required string Reason { get; init; }
}

/// <summary>
/// 描述管理员用户列表的分页和筛选条件。
/// </summary>
public sealed record AdminUserListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public string? Role { get; init; }
    public AdminUserStatus? Status { get; init; }
}

/// <summary>
/// 表示管理员用户列表支持的账户状态筛选。
/// </summary>
public enum AdminUserStatus
{
    Active,
    Banned,
    Deleted,
}

/// <summary>
/// 返回当前登录用户可见的私有账户资料。
/// </summary>
public sealed record CurrentUserProfileResponse(
    Guid Id,
    string Email,
    UserRole Role,
    string? Nickname,
    string? AvatarUrl,
    string? Bio,
    DateTimeOffset CreatedAt);

/// <summary>
/// 返回登录用户可见的最小公开资料。
/// </summary>
public sealed record PublicUserProfileResponse(
    Guid Id,
    string? Nickname,
    string? AvatarUrl,
    string? Bio);

/// <summary>
/// 返回管理员用户列表所需的账户、状态和会话摘要。
/// </summary>
public sealed record AdminUserListItemResponse(
    Guid Id,
    string Username,
    string Email,
    UserRole Role,
    string? Nickname,
    string? AvatarUrl,
    bool IsBanned,
    DateTimeOffset? BannedAt,
    string? BannedReason,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLoginAt,
    int ActiveSessionCount);

/// <summary>
/// 返回管理员查看单个用户时需要的完整管理资料和会话摘要。
/// </summary>
public sealed record AdminUserDetailResponse(
    Guid Id,
    string Username,
    string Email,
    UserRole Role,
    string? Nickname,
    string? AvatarUrl,
    string? Bio,
    bool IsBanned,
    DateTimeOffset? BannedAt,
    string? BannedReason,
    bool IsDeleted,
    DateTimeOffset? DeletedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLoginAt,
    int ActiveSessionCount);

/// <summary>
/// 返回用户当前的角色分配。
/// </summary>
/// <param name="UserId">用户标识。</param>
/// <param name="Role">分配后的角色。</param>
public sealed record UserRoleResponse(Guid UserId, UserRole Role);
