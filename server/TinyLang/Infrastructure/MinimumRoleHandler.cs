using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TinyLang.Constants;
using TinyLang.Entities.Enums;

namespace TinyLang.Infrastructure;

/// <summary>
/// 根据 JWT role claim 和角色枚举顺序评估最低角色要求。
/// </summary>
public sealed class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    /// <inheritdoc />
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, MinimumRoleRequirement requirement)
    {
        var userRoleClaim = context.User.FindFirstValue(JwtClaimNamesExtension.Role);
        if (!UserRoleParser.TryParse(userRoleClaim, out var userRole) ||
            !Enum.IsDefined(requirement.MinimumRole))
        {
            return Task.CompletedTask;
        }

        if (userRole >= requirement.MinimumRole)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
