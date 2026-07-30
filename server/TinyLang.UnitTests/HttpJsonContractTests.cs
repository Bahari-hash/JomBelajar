using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies the production Minimal API JSON options expose one strict string enum contract.
/// </summary>
public sealed class HttpJsonContractTests
{
    /// <summary>
    /// Verifies representative article, media, multipart and user enums use exact member names.
    /// </summary>
    [Fact]
    public async Task ResponsesShouldSerializeEnumsAsPascalCaseStrings()
    {
        await using var app = await CreateAppAsync();

        var response = await app.GetTestClient().GetAsync(
            "/enum-contract",
            TestContext.Current.CancellationToken);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        json.RootElement.GetProperty("articleStatus").GetString().Should().Be("Draft");
        json.RootElement.GetProperty("resourceModule").GetString().Should().Be("CourseVideo");
        json.RootElement.GetProperty("resourceStatus").GetString().Should().Be("Active");
        json.RootElement.GetProperty("multipartStatus").GetString().Should().Be("Finalizing");
        json.RootElement.GetProperty("userRole").GetString().Should().Be("Admin");
    }

    /// <summary>
    /// Verifies string enum request values bind and numeric enum request values are rejected.
    /// </summary>
    [Fact]
    public async Task RequestsShouldAcceptMemberNamesAndRejectNumbers()
    {
        await using var app = await CreateAppAsync();
        var client = app.GetTestClient();
        using var stringContent = CreateJsonContent("""
            {
              "articleStatus": "Published",
              "resourceModule": "ArticlePicture",
              "resourceStatus": "Pending",
              "multipartStatus": "Completed",
              "userRole": "Editor"
            }
            """);
        using var numericContent = CreateJsonContent("""
            {
              "articleStatus": 1,
              "resourceModule": 4,
              "resourceStatus": 0,
              "multipartStatus": 3,
              "userRole": 1
            }
            """);

        var accepted = await client.PostAsync(
            "/enum-contract",
            stringContent,
            TestContext.Current.CancellationToken);
        var rejected = await client.PostAsync(
            "/enum-contract",
            numericContent,
            TestContext.Current.CancellationToken);
        using var acceptedJson = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        accepted.StatusCode.Should().Be(HttpStatusCode.OK);
        acceptedJson.RootElement.GetProperty("articleStatus").GetString().Should().Be("Published");
        acceptedJson.RootElement.GetProperty("resourceModule").GetString().Should().Be("ArticlePicture");
        acceptedJson.RootElement.GetProperty("resourceStatus").GetString().Should().Be("Pending");
        acceptedJson.RootElement.GetProperty("multipartStatus").GetString().Should().Be("Completed");
        acceptedJson.RootElement.GetProperty("userRole").GetString().Should().Be("Editor");
        rejected.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    /// <summary>
    /// Creates a TestServer configured through the production HTTP JSON registration method.
    /// </summary>
    /// <returns>The started test application.</returns>
    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddApiJsonSerialization();
        var app = builder.Build();
        app.MapGet("/enum-contract", () => new HttpEnumContract(
            ArticleStatus.Draft,
            ResourceModule.CourseVideo,
            ResourceStatus.Active,
            MultipartUploadStatus.Finalizing,
            UserRole.Admin));
        app.MapPost("/enum-contract", (HttpEnumContract request) => request);
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>
    /// Creates UTF-8 JSON request content without using client-side enum serialization defaults.
    /// </summary>
    /// <param name="json">The exact request JSON.</param>
    /// <returns>The request content.</returns>
    private static StringContent CreateJsonContent(string json)
        => new(json, Encoding.UTF8, "application/json");
}

/// <summary>
/// Provides representative HTTP enum properties for request and response contract tests.
/// </summary>
/// <param name="ArticleStatus">The article lifecycle state.</param>
/// <param name="ResourceModule">The media ownership module.</param>
/// <param name="ResourceStatus">The media lifecycle state.</param>
/// <param name="MultipartStatus">The multipart upload lifecycle state.</param>
/// <param name="UserRole">The user authorization role.</param>
public sealed record HttpEnumContract(
    ArticleStatus ArticleStatus,
    ResourceModule ResourceModule,
    ResourceStatus ResourceStatus,
    MultipartUploadStatus MultipartStatus,
    UserRole UserRole);
