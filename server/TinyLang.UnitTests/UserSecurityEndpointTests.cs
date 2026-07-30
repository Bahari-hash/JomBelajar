using System.Linq;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class UserSecurityEndpointTests
{
    [Fact]
    public async Task UserRoutesShouldUseTheExpectedAuthorizationPolicies()
    {
        await using var app = CreateApp();
        var routes = GetRouteEndpoints(app);

        AssertPolicy(routes, "/api/users/me", AuthorizationPolicies.RequireUser);
        AssertPolicy(routes, "/api/users/me/profile", AuthorizationPolicies.RequireUser);
        AssertPolicy(routes, "/api/users/{id:guid}/profile", AuthorizationPolicies.RequireUser);
        AssertPolicy(routes, "/api/admin/users/", AuthorizationPolicies.RequireAdmin);
        AssertPolicy(routes, "/api/admin/users/{id:guid}", AuthorizationPolicies.RequireAdmin);
        AssertPolicy(routes, "/api/admin/users/{id:guid}/ban", AuthorizationPolicies.RequireAdmin);
        AssertPolicy(routes, "/api/admin/users/{id:guid}/unban", AuthorizationPolicies.RequireAdmin);
        AssertPolicy(routes, "/api/admin/users/{id:guid}/role", AuthorizationPolicies.RequireAdmin);

        routes.Should().NotContain(endpoint => endpoint.RoutePattern.RawText == "/api/users/{id:guid}/ban");
        routes.Should().NotContain(endpoint => endpoint.RoutePattern.RawText == "/api/users/{id:guid}/unban");
        routes.Should().NotContain(endpoint => endpoint.RoutePattern.RawText == "/api/users/{id:guid}/role");
    }

    [Fact]
    public async Task AccountSecurityRoutesShouldRequireAuthenticatedUsers()
    {
        await using var app = CreateApp();
        var routes = GetRouteEndpoints(app);

        AssertPolicy(routes, "/api/users/me/reset-password", AuthorizationPolicies.RequireUser);
        AssertPolicy(routes, "/api/users/me/change-email", AuthorizationPolicies.RequireUser);
        AssertPolicy(routes, "/api/users/me/delete-account", AuthorizationPolicies.RequireUser);

        routes.Should().NotContain(endpoint => endpoint.RoutePattern.RawText == "/api/users/me/password");
        routes.Should().NotContain(endpoint => endpoint.RoutePattern.RawText == "/api/users/me/email");
        routes.Should().NotContain(endpoint => endpoint.RoutePattern.RawText == "/api/delete-me");
    }

    private static WebApplication CreateApp()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Services.AddSingleton(Mock.Of<IUserService>());
        builder.Services.AddSingleton(Mock.Of<IAccountSecurityService>());
        var app = builder.Build();
        app.MapGroup("/api")
            .MapUsersApi()
            .MapSecurityApi();
        return app;
    }

    private static IReadOnlyList<RouteEndpoint> GetRouteEndpoints(WebApplication app)
        => ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .ToArray();

    private static void AssertPolicy(
        IReadOnlyCollection<RouteEndpoint> routes,
        string pattern,
        string policy)
    {
        var matching = routes.Where(endpoint => endpoint.RoutePattern.RawText == pattern).ToArray();
        matching.Should().NotBeEmpty("available routes: {0}", string.Join(", ", routes.Select(x => x.RoutePattern.RawText)));
        matching.Should().OnlyContain(endpoint => endpoint.Metadata
            .GetOrderedMetadata<IAuthorizeData>()
            .Any(data => data.Policy == policy));
    }
}
