using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using TinyLang.Constants;
using TinyLang.Entities.Enums;

namespace TinyLang.Infrastructure;

public sealed class MinimumRoleHandler : AuthorizationHandler<MinimumRoleRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, MinimumRoleRequirement requirement)
    {
        var userRoleClaim = context.User.FindFirstValue(JwtClaimNamesExtension.Role);
        if (string.IsNullOrWhiteSpace(userRoleClaim) ||
            !Enum.TryParse<UserRole>(userRoleClaim, out var userRole))
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
