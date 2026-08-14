using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TinyLang.Constants;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证 User/Admin 两级授权策略及严格 JWT role claim 边界。
/// </summary>
public sealed class AuthorizationPolicyTests
{
    [Theory]
    [InlineData("User", AuthorizationPolicies.RequireUser, true)]
    [InlineData("User", AuthorizationPolicies.RequireAdmin, false)]
    [InlineData("Admin", AuthorizationPolicies.RequireUser, true)]
    [InlineData("Admin", AuthorizationPolicies.RequireAdmin, true)]
    public async Task PoliciesShouldApplyUserAndAdminMatrix(
        string role,
        string policy,
        bool expectedSuccess)
    {
        await using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var result = await authorization.AuthorizeAsync(CreatePrincipal(role), null, policy);

        result.Succeeded.Should().Be(expectedSuccess);
    }

    [Theory]
    [InlineData("user")]
    [InlineData("admin")]
    [InlineData("Editor")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("2")]
    [InlineData("999")]
    [InlineData("")]
    public async Task PoliciesShouldRejectNonCanonicalRoleClaims(string role)
    {
        await using var provider = CreateProvider();
        var authorization = provider.GetRequiredService<IAuthorizationService>();

        var userResult = await authorization.AuthorizeAsync(
            CreatePrincipal(role), null, AuthorizationPolicies.RequireUser);
        var adminResult = await authorization.AuthorizeAsync(
            CreatePrincipal(role), null, AuthorizationPolicies.RequireAdmin);

        userResult.Succeeded.Should().BeFalse();
        adminResult.Succeeded.Should().BeFalse();
    }

    [Fact]
    public void PoliciesShouldNotRegisterRemovedEditorPolicy()
    {
        using var provider = CreateProvider();
        var options = provider.GetRequiredService<IOptions<AuthorizationOptions>>().Value;

        var policy = options.GetPolicy("RequireEditor");

        policy.Should().BeNull();
    }

    [Fact]
    public async Task HandlerShouldRejectUndefinedMinimumRole()
    {
        var requirement = new MinimumRoleRequirement((UserRole)999);
        var context = new AuthorizationHandlerContext(
            [requirement],
            CreatePrincipal("Admin"),
            resource: null);
        IAuthorizationHandler handler = new MinimumRoleHandler();

        await handler.HandleAsync(context);

        context.HasSucceeded.Should().BeFalse();
    }

    private static ServiceProvider CreateProvider()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorizationPolicy();
        return services.BuildServiceProvider();
    }

    private static ClaimsPrincipal CreatePrincipal(string role)
        => new(new ClaimsIdentity(
        [
            new Claim(JwtClaimNamesExtension.Role, role)
        ], "Test"));
}
