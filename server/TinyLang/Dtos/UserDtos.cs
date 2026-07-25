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
/// 描述管理员修改用户角色的请求。
/// </summary>
public sealed record UpdateRoleRequest
{
    public required string Role { get; init; }
}

/// <summary>
/// 返回用户的账户身份、公开资料和封禁状态。
/// </summary>
/// <param name="Id">用户标识。</param>
/// <param name="Email">用户邮箱。</param>
/// <param name="Role">用户角色。</param>
/// <param name="Nickname">用户昵称。</param>
/// <param name="AvatarUrl">头像公开地址。</param>
/// <param name="Bio">用户简介。</param>
/// <param name="IsBanned">用户是否已被封禁。</param>
public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    UserRole Role,
    string? Nickname,
    string? AvatarUrl,
    string? Bio,
    bool IsBanned);

/// <summary>
/// 返回用户当前的角色分配。
/// </summary>
/// <param name="UserId">用户标识。</param>
/// <param name="Role">分配后的角色。</param>
public sealed record UserRoleResponse(Guid UserId, UserRole Role);
