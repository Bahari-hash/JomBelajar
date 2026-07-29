using System.Linq;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class AuthEndpointTests
{
    [Fact]
    public async Task LogoutShouldUseValidatedTokenClaimsWithoutReadingAuthorizationHeader()
    {
        var userId = Guid.NewGuid();
        var tokenId = Guid.NewGuid().ToString("N");
        var expiresAt = DateTimeOffset.FromUnixTimeSeconds(
            DateTimeOffset.UtcNow.AddMinutes(10).ToUnixTimeSeconds());
        const string refreshToken = "refresh-token";
        var authService = new Mock<IAuthService>();
        await using var app = await CreateAppAsync(authService.Object, userId, tokenId, expiresAt);

        var response = await app.GetTestClient().PostAsJsonAsync(
            "/api/auth/logout",
            new LogoutRequest { RefreshToken = refreshToken },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        authService.Verify(x => x.LogoutAsync(
            userId,
            tokenId,
            expiresAt,
            refreshToken,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task AdminRevokeRouteShouldRemainAdminOnlyAndAllowSelfRevoke()
    {
        var userId = Guid.NewGuid();
        var authService = new Mock<IAuthService>();
        await using var app = await CreateAppAsync(
            authService.Object,
            userId,
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow.AddMinutes(10));
        var route = ((IEndpointRouteBuilder)app).DataSources
            .SelectMany(source => source.Endpoints)
            .OfType<RouteEndpoint>()
            .Single(endpoint => endpoint.RoutePattern.RawText ==
                "/api/auth/admin/users/{userId:guid}/revoke");

        var response = await app.GetTestClient().PostAsync(
            $"/api/auth/admin/users/{userId}/revoke",
            null,
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
        route.Metadata.GetOrderedMetadata<IAuthorizeData>()
            .Should().Contain(data => data.Policy == AuthorizationPolicies.RequireAdmin);
        authService.Verify(value => value.RevokeUserAsync(
            userId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task<WebApplication> CreateAppAsync(
        IAuthService authService,
        Guid userId,
        string tokenId,
        DateTimeOffset expiresAt)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireUser, policy =>
                policy.RequireAssertion(_ => true));
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAssertion(_ => true));
        });
        builder.Services.AddSingleton(authService);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(JwtClaimNamesExtension.UserId, userId.ToString()),
                new Claim(JwtClaimNamesExtension.TokenId, tokenId),
                new Claim(JwtClaimNamesExtension.Expiration, expiresAt.ToUnixTimeSeconds().ToString())
            ], "Test"));
            await next();
        });
        app.UseAuthorization();
        app.MapGroup("/api").MapAuthApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
