using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Endpoints;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证运行时 OpenAPI 冻结 Admin 内容、上传和播放的 breaking HTTP 契约。
/// </summary>
public sealed class OpenApiContractTests
{
    /// <summary>
    /// 验证 OpenAPI 公开当前管理路径、并发前置条件和媒体响应字段。
    /// </summary>
    [Fact]
    public async Task OpenApiShouldExposeCurrentAdminVideoContract()
    {
        await using var app = await CreateAppAsync();
        var json = await app.GetTestClient().GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        paths.TryGetProperty("/api/admin/videos", out _).Should().BeTrue();
        paths.TryGetProperty("/api/admin/videos/{id}/archive", out _).Should().BeTrue();
        paths.TryGetProperty("/api/admin/videos/{id}/playback", out _).Should().BeTrue();
        paths.TryGetProperty("/api/admin/video-categories/{id}/videos", out _)
            .Should().BeTrue();
        paths.TryGetProperty("/api/admin/words", out _).Should().BeTrue();
        paths.TryGetProperty("/api/admin/words/batch/validate", out _)
            .Should().BeFalse();
        paths.TryGetProperty("/api/admin/words/batch", out _).Should().BeFalse();
        paths.TryGetProperty("/api/admin/words/{id}/archive", out _)
            .Should().BeTrue();
        paths.TryGetProperty("/api/words", out _).Should().BeTrue();
        paths.TryGetProperty("/api/admin/papers/{paperId}/validate", out _)
            .Should().BeTrue();
        paths.TryGetProperty("/api/admin/papers/{paperId}/archive", out _)
            .Should().BeTrue();
        paths.TryGetProperty(
            "/api/paper-attempts/{attemptId}/answers/{questionId}",
            out var answerPath).Should().BeTrue();
        answerPath.TryGetProperty("delete", out _).Should().BeTrue();
        paths.TryGetProperty("/api/uploads/admin/media/capabilities", out _)
            .Should().BeTrue();
        foreach (var path in new[]
        {
            "/api/uploads/admin/media/multipart",
            "/api/uploads/admin/multipart/{sessionId}/parts/presign",
            "/api/uploads/admin/multipart/{sessionId}",
            "/api/uploads/admin/multipart/{sessionId}/complete"
        })
        {
            paths.TryGetProperty(path, out _).Should().BeTrue();
        }
        paths.EnumerateObject().Should().NotContain(path =>
            path.Name.StartsWith("/api/editor", StringComparison.Ordinal) ||
            path.Name.StartsWith("/api/uploads/multipart", StringComparison.Ordinal) ||
            path.Name.Contains("subtitles", StringComparison.Ordinal));

        var schemas = document.RootElement
            .GetProperty("components")
            .GetProperty("schemas");
        var updateRequest = GetSchema(schemas, "UpdateVideoRequest");
        var mutationRequest = GetSchema(schemas, "VideoMutationRequest");
        GetRequiredProperties(updateRequest).Should().Contain("concurrencyStamp");
        GetRequiredProperties(mutationRequest).Should().Contain("concurrencyStamp");

        var wordMutationRequest = GetSchema(schemas, "WordMutationRequest");
        GetRequiredProperties(wordMutationRequest).Should().Contain("concurrencyStamp");
        var wordStatus = GetSchema(schemas, "WordPublicationStatus");
        wordStatus.GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Contain("Archived");
        var adminWord = GetSchema(schemas, "AdminWordResponse");
        adminWord.GetProperty("properties").TryGetProperty("archivedAt", out _)
            .Should().BeTrue();
        adminWord.GetProperty("properties").TryGetProperty("concurrencyStamp", out _)
            .Should().BeTrue();
        schemas.EnumerateObject().Should().NotContain(schema =>
            schema.Name.EndsWith("BatchWordRequest", StringComparison.Ordinal));
        schemas.EnumerateObject().Should().NotContain(schema =>
            schema.Name.EndsWith("BatchWordImportResponse", StringComparison.Ordinal));

        var paperMutationRequest = GetSchema(schemas, "PaperMutationRequest");
        GetRequiredProperties(paperMutationRequest).Should().Contain("concurrencyStamp");
        var paperStatus = GetSchema(schemas, "PaperPublicationStatus");
        paperStatus.GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Contain("Archived");
        var adminPaper = GetSchema(schemas, "AdminPaperResponse");
        var adminPaperProperties = adminPaper.GetProperty("properties");
        adminPaperProperties.TryGetProperty("attemptCount", out _).Should().BeTrue();
        adminPaperProperties.TryGetProperty("createdBy", out _).Should().BeTrue();
        adminPaperProperties.TryGetProperty("lastEditor", out _).Should().BeTrue();
        adminPaperProperties.TryGetProperty("archivedAt", out _).Should().BeTrue();
        var validationIssue = GetSchema(schemas, "PaperValidationIssueResponse")
            .GetProperty("properties");
        validationIssue.TryGetProperty("field", out _).Should().BeTrue();
        validationIssue.TryGetProperty("errorCode", out _).Should().BeTrue();
        validationIssue.TryGetProperty("questionId", out _).Should().BeTrue();
        var userAttempt = GetSchema(schemas, "UserPaperAttemptResponse")
            .GetProperty("properties");
        userAttempt.TryGetProperty("paperTotalScore", out _).Should().BeTrue();
        userAttempt.TryGetProperty("paperPassingScore", out _).Should().BeTrue();
        GetSchema(schemas, "UserPaperAttemptQuestionResponse")
            .GetProperty("properties").TryGetProperty("points", out _)
            .Should().BeTrue();

        var adminVideo = GetSchema(schemas, "AdminVideoResponse");
        var videoPlayback = GetSchema(schemas, "VideoPlaybackResponse");
        var audioPlayback = GetSchema(schemas, "AudioResourcePlaybackResponse");
        adminVideo.GetProperty("properties").TryGetProperty("subtitles", out _)
            .Should().BeFalse();
        videoPlayback.GetProperty("properties").TryGetProperty("subtitles", out _)
            .Should().BeFalse();
        IsNullable(videoPlayback.GetProperty("properties").GetProperty("expiresAt"))
            .Should().BeTrue();
        IsNullable(audioPlayback.GetProperty("properties").GetProperty("expiresAt"))
            .Should().BeTrue();

        var module = GetSchema(schemas, "ResourceModule");
        var moduleValues = module.GetProperty("enum").EnumerateArray().ToArray();
        moduleValues.Should().OnlyContain(value => value.ValueKind == JsonValueKind.String);
        moduleValues
            .Select(value => value.GetString())
            .Should().BeEquivalentTo(
                "Avatar",
                "ArticlePicture",
                "VideoCover",
                "Audio",
                "CourseVideo");
    }

    [Fact]
    public async Task OpenApiShouldExposeAudioResourceContractWithoutAudioClipTypes()
    {
        await using var app = await CreateAppAsync();
        var json = await app.GetTestClient().GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");

        foreach (var path in new[]
        {
            "/api/admin/audio",
            "/api/admin/audio/{id}",
            "/api/admin/audio/{id}/name",
            "/api/admin/audio/{id}/retry-upload",
            "/api/admin/audio/{id}/reprocess",
            "/api/audio/{id}/playback"
        })
        {
            paths.TryGetProperty(path, out _).Should().BeTrue();
        }

        paths.EnumerateObject().Should().NotContain(path =>
            path.Name.StartsWith("/api/admin/audio", StringComparison.Ordinal) &&
            (path.Name.Contains("/publish", StringComparison.Ordinal) ||
             path.Name.Contains("/unpublish", StringComparison.Ordinal) ||
             path.Name.EndsWith("/retry", StringComparison.Ordinal)));
        document.RootElement.GetProperty("components").GetProperty("schemas")
            .EnumerateObject().Should().NotContain(schema =>
                schema.Name.Contains("AudioClip", StringComparison.Ordinal));
    }

    /// <summary>
    /// 创建只承载目标 endpoints 和 OpenAPI 文档的内存 Web 应用。
    /// </summary>
    private static async Task<WebApplication> CreateAppAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddOpenApi();
        builder.Services.AddApiJsonSerialization();
        builder.Services.AddAuthorizationPolicy();
        builder.Services.AddSingleton(Mock.Of<IVideoService>());
        builder.Services.AddSingleton(Mock.Of<IVideoCategoryService>());
        builder.Services.AddSingleton(Mock.Of<IAudioResourceService>());
        builder.Services.AddSingleton(Mock.Of<IWordService>());
        builder.Services.AddSingleton(Mock.Of<IPaperService>());
        builder.Services.AddSingleton(Mock.Of<IPaperAttemptService>());
        builder.Services.AddSingleton(Mock.Of<IMediaResourceService>());
        var app = builder.Build();
        app.MapOpenApi();
        app.MapGroup("/api")
            .MapUploadsApi()
            .MapVideoCategoriesApi()
            .MapVideosApi()
            .MapAudioApi()
            .MapWordsApi()
            .MapOnlineQuizApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>
    /// 按稳定 DTO 后缀查找 OpenAPI schema，兼容生成器附加命名空间前缀。
    /// </summary>
    private static JsonElement GetSchema(JsonElement schemas, string name)
        => schemas.EnumerateObject()
            .Single(schema => schema.Name.EndsWith(name, StringComparison.Ordinal))
            .Value;

    /// <summary>
    /// 返回 schema 中显式声明为 required 的 JSON 属性名。
    /// </summary>
    private static string[] GetRequiredProperties(JsonElement schema)
        => schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [];

    /// <summary>
    /// 判断 OpenAPI 3.0/3.1 schema 是否允许 null。
    /// </summary>
    private static bool IsNullable(JsonElement schema)
    {
        if (schema.TryGetProperty("nullable", out var nullable) && nullable.GetBoolean())
        {
            return true;
        }
        if (GetSchemaTypes(schema).Contains("null", StringComparer.Ordinal))
        {
            return true;
        }
        return schema.TryGetProperty("anyOf", out var anyOf) &&
            anyOf.EnumerateArray().Any(value =>
                GetSchemaTypes(value).Contains("null", StringComparer.Ordinal));
    }

    /// <summary>
    /// 读取 OpenAPI 3.0 单值或 3.1 数组形式的 type。
    /// </summary>
    private static string[] GetSchemaTypes(JsonElement schema)
    {
        if (!schema.TryGetProperty("type", out var type))
        {
            return [];
        }
        return type.ValueKind == JsonValueKind.Array
            ? type.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [type.GetString()!];
    }
}
