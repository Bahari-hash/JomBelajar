using Microsoft.AspNetCore.Authorization;
using TinyLang.Entities.Enums;

namespace TinyLang.Infrastructure;

/// <summary>
/// 要求已认证用户至少具有指定的 TinyLang 角色层级。
/// </summary>
/// <param name="userRole">满足授权所需的最低角色。</param>
public sealed class MinimumRoleRequirement(
    UserRole userRole) : IAuthorizationRequirement
{
    public UserRole MinimumRole { get; } = userRole;
}
