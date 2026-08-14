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
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Services;
using TinyLang.Settings;

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

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(
            "Cookie",
            $"tiny-lang.refresh={refreshToken}");
        var response = await client.PostAsJsonAsync(
            "/api/auth/logout",
            new { },
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

    [Fact]
    public async Task LoginShouldReturnBrowserSessionAndSetHttpOnlyRefreshCookie()
    {
        var authService = new Mock<IAuthService>();
        var issueResult = new AuthTokenIssueResult(
            "access-token",
            "refresh-token",
            900,
            new UserResponse(
                Guid.NewGuid(),
                "learner@example.test",
                UserRole.User));
        authService
            .Setup(service => service.LoginAsync(
                It.IsAny<LoginRequest>(),
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(issueResult);
        await using var app = await CreateAppAsync(
            authService.Object,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow.AddMinutes(10),
            new JwtSettings
            {
                JwtSecret = new string('s', 32),
                Issuer = "TinyLang.Backend",
                Audience = "TinyLang.Frontend",
                AccessTokenExpMinutes = 15,
                RefreshTokenExpMinutes = 10080
            });

        var client = app.GetTestClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new
            {
                email = "learner@example.test",
                password = "secret-password"
            },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthSessionResponse>(
            TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Token.Should().Be("access-token");
        body.User.Email.Should().Be("learner@example.test");
        var responseText = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        responseText.Should().NotContain("refresh-token");
        response.Headers.GetValues("Set-Cookie").Should().ContainSingle(
            cookie => cookie.StartsWith("tiny-lang.refresh=refresh-token;", StringComparison.Ordinal) &&
                cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("SameSite=lax", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("path=/api/auth", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task RefreshShouldReadCookieAndRotateWithoutReturningRefreshToken()
    {
        var authService = new Mock<IAuthService>();
        var issueResult = new AuthTokenIssueResult(
            "new-access-token",
            "new-refresh-token",
            900,
            new UserResponse(
                Guid.NewGuid(),
                "learner@example.test",
                UserRole.User));
        authService
            .Setup(service => service.RefreshAsync(
                "current-refresh-token",
                It.IsAny<string?>(),
                It.IsAny<string?>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(issueResult);
        await using var app = await CreateAppAsync(
            authService.Object,
            Guid.NewGuid(),
            Guid.NewGuid().ToString("N"),
            DateTimeOffset.UtcNow.AddMinutes(10),
            new JwtSettings
            {
                JwtSecret = new string('s', 32),
                Issuer = "TinyLang.Backend",
                Audience = "TinyLang.Frontend",
                AccessTokenExpMinutes = 15,
                RefreshTokenExpMinutes = 10080
            });

        var client = app.GetTestClient();
        client.DefaultRequestHeaders.Add(
            "Cookie",
            "tiny-lang.refresh=current-refresh-token");
        var response = await client.PostAsJsonAsync(
            "/api/auth/refresh",
            new { },
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<AuthSessionResponse>(
            TestContext.Current.CancellationToken);
        body.Should().NotBeNull();
        body!.Token.Should().Be("new-access-token");
        var responseText = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        responseText.Should().NotContain("new-refresh-token");
        response.Headers.GetValues("Set-Cookie").Should().ContainSingle(
            cookie => cookie.StartsWith("tiny-lang.refresh=new-refresh-token;", StringComparison.Ordinal) &&
                cookie.Contains("httponly", StringComparison.OrdinalIgnoreCase) &&
                cookie.Contains("SameSite=lax", StringComparison.OrdinalIgnoreCase));
        authService.Verify(service => service.RefreshAsync(
            "current-refresh-token",
            It.IsAny<string?>(),
            It.IsAny<string?>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ForgotPasswordHandlersShouldDelegateWithoutAuthenticationState()
    {
        var authService = new Mock<IAuthService>();
        var securityService = new Mock<IAccountSecurityService>();
        var tokenRequest = new ForgotPasswordTokenRequest
        {
            Email = "learner@example.test"
        };
        var resetRequest = new ForgotPasswordRequest
        {
            Email = "learner@example.test",
            NewPassword = "new-password",
            VerificationCode = "123456"
        };

        var tokenResult = await AuthEndpoints.SendForgotPasswordTokenAsync(
            tokenRequest,
            authService.Object,
            TestContext.Current.CancellationToken);
        var resetResult = await AuthEndpoints.ResetForgottenPasswordAsync(
            resetRequest,
            securityService.Object,
            TestContext.Current.CancellationToken);

        tokenResult.StatusCode.Should().Be((int)HttpStatusCode.OK);
        resetResult.StatusCode.Should().Be((int)HttpStatusCode.NoContent);
        authService.Verify(service => service.SendForgotPasswordTokenAsync(
            tokenRequest.Email,
            It.IsAny<CancellationToken>()), Times.Once);
        securityService.Verify(service => service.ResetForgottenPasswordAsync(
            resetRequest,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    private static async Task<WebApplication> CreateAppAsync(
        IAuthService authService,
        Guid userId,
        string tokenId,
        DateTimeOffset expiresAt,
        JwtSettings? jwtSettings = null)
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
        builder.Services.AddSingleton(Mock.Of<IAccountSecurityService>());
        builder.Services.AddSingleton(
            Options.Create(jwtSettings ?? new JwtSettings
            {
                JwtSecret = new string('s', 32),
                Issuer = "TinyLang.Backend",
                Audience = "TinyLang.Frontend",
                AccessTokenExpMinutes = 15,
                RefreshTokenExpMinutes = 10080
            }));
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
