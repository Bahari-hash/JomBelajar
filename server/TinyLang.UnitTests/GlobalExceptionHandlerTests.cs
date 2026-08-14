using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TinyLang.Exceptions;
using TinyLang.Middlewares;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies the global exception boundary emits safe RFC Problem Details with stable error codes.
/// </summary>
public sealed class GlobalExceptionHandlerTests
{
    /// <summary>
    /// Verifies a business conflict exposes its stable code even when it uses a custom detail.
    /// </summary>
    [Fact]
    public async Task BusinessConflictShouldReturnStableErrorCode()
    {
        const string customDetail = "The article changed while it was being edited.";
        await using var app = await CreateAppAsync(ConflictException.Create(
            ErrorCodes.ArticleConcurrencyConflict,
            customDetail));

        var response = await app.GetTestClient().GetAsync(
            "/failure",
            TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problem.RootElement.GetProperty("status").GetInt32().Should().Be(409);
        problem.RootElement.GetProperty("detail").GetString().Should().Be(customDetail);
        problem.RootElement.GetProperty("instance").GetString().Should().Be("/failure");
        problem.RootElement.GetProperty("errorCode").GetString()
            .Should().Be(nameof(ErrorCodes.ArticleConcurrencyConflict));
    }

    /// <summary>
    /// Verifies validation responses retain both the top-level code and existing field message structure.
    /// </summary>
    [Fact]
    public async Task ValidationFailureShouldReturnErrorCodeAndFieldErrors()
    {
        var errors = new Dictionary<string, string[]>
        {
            ["ContentMarkdown"] = ["Article content is required."]
        };
        await using var app = await CreateAppAsync(new RequestValidationException(
            ErrorCodes.RequestValidationFailed,
            errors));

        var response = await app.GetTestClient().GetAsync(
            "/failure",
            TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        problem.RootElement.GetProperty("errorCode").GetString()
            .Should().Be(nameof(ErrorCodes.RequestValidationFailed));
        problem.RootElement.GetProperty("errors")
            .GetProperty("ContentMarkdown")[0]
            .GetString().Should().Be("Article content is required.");
    }

    /// <summary>
    /// Verifies unknown exceptions use the generic code without exposing internal exception details.
    /// </summary>
    [Fact]
    public async Task UnknownFailureShouldReturnUnexpectedErrorWithoutLeakingDetails()
    {
        const string internalMessage = "database-password-and-provider-details";
        await using var app = await CreateAppAsync(new InvalidOperationException(internalMessage));

        var response = await app.GetTestClient().GetAsync(
            "/failure",
            TestContext.Current.CancellationToken);
        var content = await response.Content.ReadAsStringAsync(
            TestContext.Current.CancellationToken);
        using var problem = JsonDocument.Parse(content);

        response.StatusCode.Should().Be(HttpStatusCode.InternalServerError);
        problem.RootElement.GetProperty("errorCode").GetString()
            .Should().Be(nameof(ErrorCodes.UnexpectedError));
        content.Should().NotContain(internalMessage);
    }

    /// <summary>
    /// Creates a TestServer that routes a supplied exception through the production handler.
    /// </summary>
    /// <param name="exception">The exception raised by the test endpoint.</param>
    /// <returns>The started test application.</returns>
    private static async Task<WebApplication> CreateAppAsync(Exception exception)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Services.AddProblemDetails();
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        var app = builder.Build();
        app.UseExceptionHandler();
        app.MapGet("/failure", () => Task.FromException(exception));
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }
}
