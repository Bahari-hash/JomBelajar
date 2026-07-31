using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证用户管理 HTTP 参数绑定、身份传递和响应契约。
/// </summary>
public sealed class UserEndpointTests
{
    [Fact]
    public async Task AdminListShouldBindPaginationAndStrictStringFilters()
    {
        var userService = new Mock<IUserService>();
        userService.Setup(value => value.GetAdminListAsync(
                It.IsAny<AdminUserListRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PagedResponse<AdminUserListItemResponse>([], 2, 5, 0, 0));
        await using var app = await CreateHttpAppAsync(userService.Object);

        var response = await app.GetTestClient().GetAsync(
            "/api/admin/users?page=2&pageSize=5&keyword=alice&role=Admin&status=Banned",
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        userService.Verify(value => value.GetAdminListAsync(
            It.Is<AdminUserListRequest>(request =>
                request.Page == 2 &&
                request.PageSize == 5 &&
                request.Keyword == "alice" &&
                request.Role == "Admin" &&
                request.Status == AdminUserStatus.Banned),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CurrentProfileAndBanShouldUseAuthenticatedUserId()
    {
        var operatorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        userService.Setup(value => value.GetCurrentProfileAsync(
                operatorId,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new CurrentUserProfileResponse(
                operatorId,
                "admin@example.test",
                UserRole.Admin,
                "Admin",
                null,
                null,
                DateTimeOffset.UtcNow));
        await using var app = await CreateHttpAppAsync(userService.Object, operatorId);

        var profileResponse = await app.GetTestClient().GetAsync(
            "/api/users/me",
            TestContext.Current.CancellationToken);
        var banResponse = await app.GetTestClient().PostAsJsonAsync(
            $"/api/admin/users/{targetId}/ban",
            new BanUserRequest { Reason = "policy violation" },
            TestContext.Current.CancellationToken);

        profileResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        banResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
        userService.Verify(value => value.GetCurrentProfileAsync(
            operatorId,
            It.IsAny<CancellationToken>()), Times.Once);
        userService.Verify(value => value.BanAsync(
            operatorId,
            targetId,
            It.Is<BanUserRequest>(request => request.Reason == "policy violation"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task RoleUpdateShouldReturnStringEnumResponseContract()
    {
        var operatorId = Guid.NewGuid();
        var targetId = Guid.NewGuid();
        var userService = new Mock<IUserService>();
        userService.Setup(value => value.UpdateRoleAsync(
                operatorId,
                targetId,
                It.IsAny<UpdateRoleRequest>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserRoleResponse(targetId, UserRole.Admin));
        await using var app = await CreateHttpAppAsync(userService.Object, operatorId);

        var response = await app.GetTestClient().PostAsJsonAsync(
            $"/api/admin/users/{targetId}/role",
            new UpdateRoleRequest { Role = "Admin" },
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        content.Should().Contain("\"role\":\"Admin\"");
        userService.Verify(value => value.UpdateRoleAsync(
            operatorId,
            targetId,
            It.Is<UpdateRoleRequest>(request => request.Role == "Admin"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 创建绕过实际 JWT 验证但保留授权和 claims 行为的 TestServer。
    /// </summary>
    private static async Task<WebApplication> CreateHttpAppAsync(
        IUserService userService,
        Guid? userId = null)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiJsonSerialization();
        builder.Services.AddAuthorization(options =>
        {
            options.AddPolicy(AuthorizationPolicies.RequireUser, policy =>
                policy.RequireAssertion(_ => true));
            options.AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
                policy.RequireAssertion(_ => true));
        });
        builder.Services.AddSingleton(userService);
        var app = builder.Build();
        app.Use(async (context, next) =>
        {
            context.User = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(
                    JwtClaimNamesExtension.UserId,
                    (userId ?? Guid.NewGuid()).ToString())
            ], "Test"));
            await next();
        });
        app.UseAuthorization();
        app.MapGroup("/api").MapUsersApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
