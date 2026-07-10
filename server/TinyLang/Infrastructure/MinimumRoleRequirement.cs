using Microsoft.AspNetCore.Authorization;
using TinyLang.Entities.Enums;

namespace TinyLang.Infrastructure;

public sealed class MinimumRoleRequirement(
    UserRole userRole) : IAuthorizationRequirement
{
    public UserRole MinimumRole { get; } = userRole;
}
