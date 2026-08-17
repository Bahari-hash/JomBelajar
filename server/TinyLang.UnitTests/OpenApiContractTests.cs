using System.Linq;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using TinyLang.Endpoints;
using TinyLang.Exceptions;
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
        foreach (var action in new[] { "publish", "unpublish", "archive" })
        {
            paths.TryGetProperty($"/api/admin/words/{{id}}/{action}", out _)
                .Should().BeFalse();
        }
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

        var deleteWordRequest = GetSchema(schemas, "DeleteWordRequest");
        GetRequiredProperties(deleteWordRequest).Should().Contain("concurrencyStamp");
        foreach (var requestName in new[] { "CreateWordRequest", "UpdateWordRequest" })
        {
            var request = GetSchema(schemas, requestName);
            var requestProperties = request.GetProperty("properties");
            AssertNullableUuid(requestProperties.GetProperty("audioResourceId"), schemas);
            requestProperties.GetProperty("senses").GetProperty("type").GetString()
                .Should().Be("array");
        }
        GetRequiredProperties(GetSchema(schemas, "UpdateWordRequest"))
            .Should().Contain("concurrencyStamp");
        var adminWord = GetSchema(schemas, "AdminWordResponse");
        var adminWordProperties = adminWord.GetProperty("properties");
        adminWordProperties.TryGetProperty("concurrencyStamp", out _)
            .Should().BeTrue();
        var adminWordAudio = adminWordProperties.GetProperty("audio");
        IsNullable(adminWordAudio).Should().BeTrue();
        GetReferencedSchemas(adminWordAudio, schemas)
            .Should().Contain("AdminWordAudioResponse")
            .And.Contain("AudioResourceStatus");
        adminWordProperties.EnumerateObject().Select(property => property.Name)
            .Should().NotContain(new[]
            {
                "status",
                "createdBy",
                "lastEditor",
                "publishedAt",
                "archivedAt",
                "pronunciations"
            });
        schemas.EnumerateObject().Select(schema => schema.Name)
            .Should().NotContain(name => new[]
            {
                "WordPublicationStatus",
                "WordPronunciationInput",
                "AdminWordPronunciationResponse",
                "WordPronunciationResponse"
            }.Any(retired => name.EndsWith(retired, StringComparison.Ordinal)));
        schemas.EnumerateObject().Should().NotContain(schema =>
            schema.Name.EndsWith("BatchWordRequest", StringComparison.Ordinal));
        schemas.EnumerateObject().Should().NotContain(schema =>
            schema.Name.EndsWith("BatchWordImportResponse", StringComparison.Ordinal));

        foreach (var responseName in new[]
                 {
                     "WordStudyNextItemResponse",
                     "WordStudySessionItemContentResponse"
                 })
        {
            var responseProperties = GetSchema(schemas, responseName)
                .GetProperty("properties");
            AssertNullableUuid(responseProperties.GetProperty("audioResourceId"), schemas);
            responseProperties.TryGetProperty("pronunciations", out _).Should().BeFalse();
        }
        GetSchema(schemas, "ExampleSentenceResponse")
            .GetProperty("properties")
            .TryGetProperty("audioClipId", out _)
            .Should().BeFalse();

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

        var expectedOperations = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["/api/admin/audio"] = ["get"],
            ["/api/admin/audio/uploads/simple"] = ["post"],
            ["/api/admin/audio/uploads/multipart"] = ["post"],
            ["/api/admin/audio/{id}"] = ["delete", "get"],
            ["/api/admin/audio/{id}/upload/confirm"] = ["put"],
            ["/api/admin/audio/{id}/name"] = ["patch"],
            ["/api/admin/audio/{id}/retry-upload"] = ["post"],
            ["/api/admin/audio/{id}/reprocess"] = ["post"],
            ["/api/admin/audio/multipart/{sessionId}/parts/presign"] = ["post"],
            ["/api/admin/audio/multipart/{sessionId}"] = ["delete", "get"],
            ["/api/admin/audio/multipart/{sessionId}/complete"] = ["post"],
            ["/api/audio/{id}/playback"] = ["post"]
        };
        var httpMethods = new HashSet<string>(
            ["delete", "get", "head", "options", "patch", "post", "put", "trace"],
            StringComparer.Ordinal);
        var actualOperations = paths.EnumerateObject()
            .Where(path => path.Name.Contains("/audio", StringComparison.Ordinal))
            .ToDictionary(
                path => path.Name,
                path => path.Value.EnumerateObject()
                    .Select(operation => operation.Name)
                    .Where(httpMethods.Contains)
                    .Order(StringComparer.Ordinal)
                    .ToArray(),
                StringComparer.Ordinal);
        actualOperations.Keys.Should().BeEquivalentTo(expectedOperations.Keys);
        foreach (var (path, methods) in expectedOperations)
        {
            actualOperations[path].Should().Equal(methods);
        }

        paths.EnumerateObject().Should().NotContain(path =>
            path.Name.StartsWith("/api/admin/audio", StringComparison.Ordinal) &&
            (path.Name.Contains("/publish", StringComparison.Ordinal) ||
             path.Name.Contains("/unpublish", StringComparison.Ordinal) ||
             path.Name.EndsWith("/retry", StringComparison.Ordinal)));
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        var status = GetSchema(schemas, "AudioResourceStatus");
        status.GetProperty("enum").EnumerateArray()
            .Select(value => value.GetString())
            .Should().Equal("Uploading", "Queued", "Processing", "Ready", "Failed");
        foreach (var schema in new[]
        {
            "InitializeAudioUploadRequest",
            "AudioUploadInitializationResponse",
            "AdminAudioResourceListItemResponse",
            "AdminAudioResourceResponse",
            "RenameAudioResourceRequest",
            "MultipartPartPresignRequest",
            "MultipartPartPresignResponse",
            "MultipartUploadStatusResponse",
            "CompleteMultipartUploadRequest",
            "AudioResourcePlaybackResponse"
        })
        {
            GetSchema(schemas, schema).ValueKind.Should().Be(JsonValueKind.Object);
        }

        foreach (var (path, method, schema) in new[]
        {
            ("/api/admin/audio/uploads/simple", "post", "InitializeAudioUploadRequest"),
            ("/api/admin/audio/uploads/multipart", "post", "InitializeAudioUploadRequest"),
            ("/api/admin/audio/{id}/name", "patch", "RenameAudioResourceRequest"),
            ("/api/admin/audio/{id}/retry-upload", "post", "InitializeAudioUploadRequest"),
            ("/api/admin/audio/multipart/{sessionId}/parts/presign", "post", "MultipartPartPresignRequest"),
            ("/api/admin/audio/multipart/{sessionId}/complete", "post", "CompleteMultipartUploadRequest")
        })
        {
            GetReferencedSchemas(
                    paths.GetProperty(path).GetProperty(method).GetProperty("requestBody"),
                    schemas)
                .Should().Contain(schema);
        }
        foreach (var (path, method, schema) in new[]
        {
            ("/api/admin/audio/uploads/simple", "post", "AudioUploadInitializationResponse"),
            ("/api/admin/audio/uploads/multipart", "post", "AudioUploadInitializationResponse"),
            ("/api/admin/audio", "get", "AdminAudioResourceListItemResponse"),
            ("/api/admin/audio/{id}", "get", "AdminAudioResourceResponse"),
            ("/api/admin/audio/{id}/name", "patch", "AdminAudioResourceResponse"),
            ("/api/admin/audio/{id}/retry-upload", "post", "AudioUploadInitializationResponse"),
            ("/api/admin/audio/{id}/reprocess", "post", "AdminAudioResourceResponse"),
            ("/api/admin/audio/multipart/{sessionId}/parts/presign", "post", "MultipartPartPresignResponse"),
            ("/api/admin/audio/multipart/{sessionId}", "get", "MultipartUploadStatusResponse"),
            ("/api/admin/audio/multipart/{sessionId}/complete", "post", "MultipartUploadStatusResponse"),
            ("/api/audio/{id}/playback", "post", "AudioResourcePlaybackResponse")
        })
        {
            GetSuccessResponseSchemas(paths.GetProperty(path).GetProperty(method), schemas)
                .Should().Contain(schema);
        }

        schemas.EnumerateObject().Should().NotContain(schema =>
            schema.Name.Contains("AudioClip", StringComparison.Ordinal) ||
            schema.Name.Contains("AudioPublicationStatus", StringComparison.Ordinal) ||
            schema.Name.Contains("WordPronunciationAudio", StringComparison.Ordinal));
        schemas.EnumerateObject().Should().NotContain(schema =>
            ContainsProperty(schema.Value, "audioClipId"));
        Enum.GetNames<ErrorCodes>().Should().NotContain(name =>
            name.StartsWith("WordPronunciationAudio", StringComparison.Ordinal));
    }

    [Fact]
    public async Task OpenApiShouldExposeArticleReadingAudioThroughSharedAudioContract()
    {
        await using var app = await CreateAppAsync();
        var json = await app.GetTestClient().GetStringAsync(
            "/openapi/v1.json",
            TestContext.Current.CancellationToken);
        using var document = JsonDocument.Parse(json);
        var paths = document.RootElement.GetProperty("paths");
        var schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        foreach (var requestName in new[] { "CreateArticleRequest", "UpdateArticleRequest" })
        {
            var property = GetSchema(schemas, requestName)
                .GetProperty("properties")
                .GetProperty("readingAudioResourceId");
            AssertNullableUuid(property, schemas);
        }

        var adminReadingAudio = GetSchema(schemas, "AdminArticleResponse")
            .GetProperty("properties")
            .GetProperty("readingAudio");
        IsNullable(adminReadingAudio).Should().BeTrue(
            "readingAudio schema was {0}",
            adminReadingAudio.GetRawText());
        GetReferencedSchemas(adminReadingAudio, schemas)
            .Should().BeEquivalentTo("ArticleReadingAudioResponse", "AudioResourceStatus");

        var publicReadingAudioId = GetSchema(schemas, "PublicArticleResponse")
            .GetProperty("properties")
            .GetProperty("readingAudioResourceId");
        AssertNullableUuid(publicReadingAudioId, schemas);

        var readingAudio = GetSchema(schemas, "ArticleReadingAudioResponse");
        GetReferencedSchemas(
                readingAudio.GetProperty("properties").GetProperty("status"),
                schemas)
            .Should().Contain("AudioResourceStatus");
        GetSchema(schemas, "AudioResourceStatus")
            .GetProperty("enum")
            .EnumerateArray()
            .Select(value => value.GetString())
            .Should().Equal("Uploading", "Queued", "Processing", "Ready", "Failed");

        paths.EnumerateObject().Should().NotContain(path =>
            path.Name.Contains("/articles", StringComparison.Ordinal) &&
            new[] { "audio", "playback", "upload" }.Any(term =>
                path.Name.Contains(term, StringComparison.OrdinalIgnoreCase)));

        GetAudioProperties(GetSchema(schemas, "CreateArticleRequest"))
            .Should().Equal("readingAudioResourceId");
        GetAudioProperties(GetSchema(schemas, "UpdateArticleRequest"))
            .Should().Equal("readingAudioResourceId");
        GetAudioProperties(GetSchema(schemas, "AdminArticleResponse"))
            .Should().Equal("readingAudio");
        GetAudioProperties(GetSchema(schemas, "PublicArticleResponse"))
            .Should().Equal("readingAudioResourceId");
        foreach (var schemaName in new[]
        {
            "CreateArticleRequest",
            "UpdateArticleRequest",
            "AdminArticleResponse",
            "PublicArticleResponse"
        })
        {
            GetSchema(schemas, schemaName).GetProperty("properties").EnumerateObject()
                .Select(property => property.Name)
                .Should().NotContain(name => IsStorageOrUploadProperty(name));
        }
        readingAudio.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .Should().BeEquivalentTo(
                "id",
                "name",
                "status",
                "durationSeconds",
                "lastFailureCode");
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
        builder.Services.AddSingleton(Mock.Of<IArticleService>());
        builder.Services.AddSingleton(Mock.Of<IArticleCategoryService>());
        builder.Services.AddSingleton(Mock.Of<IVideoService>());
        builder.Services.AddSingleton(Mock.Of<IVideoCategoryService>());
        builder.Services.AddSingleton(Mock.Of<IAudioResourceService>());
        builder.Services.AddSingleton(Mock.Of<IWordService>());
        builder.Services.AddSingleton(Mock.Of<IWordStudyService>());
        builder.Services.AddSingleton(Mock.Of<IPaperService>());
        builder.Services.AddSingleton(Mock.Of<IPaperAttemptService>());
        builder.Services.AddSingleton(Mock.Of<IMediaResourceService>());
        var app = builder.Build();
        app.MapOpenApi();
        app.MapGroup("/api")
            .MapUploadsApi()
            .MapArticlesApi()
            .MapVideoCategoriesApi()
            .MapVideosApi()
            .MapAudioApi()
            .MapWordsApi()
            .MapWordStudyApi()
            .MapOnlineQuizApi();
        await app.StartAsync(TestContext.Current.CancellationToken);
        return app;
    }

    /// <summary>
    /// 按稳定 DTO 后缀查找 OpenAPI schema，兼容生成器附加命名空间前缀。
    /// </summary>
    private static JsonElement GetSchema(JsonElement schemas, string name)
    {
        var exactMatch = schemas.EnumerateObject()
            .SingleOrDefault(schema => schema.Name.Equals(name, StringComparison.Ordinal));
        return exactMatch.Value.ValueKind != JsonValueKind.Undefined
            ? exactMatch.Value
            : schemas.EnumerateObject()
                .Single(schema => schema.Name.EndsWith(name, StringComparison.Ordinal))
                .Value;
    }

    /// <summary>
    /// 返回 schema 中显式声明为 required 的 JSON 属性名。
    /// </summary>
    private static string[] GetRequiredProperties(JsonElement schema)
        => schema.TryGetProperty("required", out var required)
            ? required.EnumerateArray().Select(value => value.GetString()!).ToArray()
            : [];

    private static string[] GetAudioProperties(JsonElement schema)
        => schema.GetProperty("properties").EnumerateObject()
            .Select(property => property.Name)
            .Where(name => name.Contains("audio", StringComparison.OrdinalIgnoreCase))
            .ToArray();

    private static void AssertNullableUuid(JsonElement property, JsonElement schemas)
    {
        IsNullable(property).Should().BeTrue();
        property.GetProperty("format").GetString().Should().Be("uuid");
        GetReferencedSchemas(property, schemas).Should().BeEmpty();
    }

    private static bool IsStorageOrUploadProperty(string name)
        => new[] { "object", "upload", "presign", "storage", "staging" }.Any(term =>
               name.Contains(term, StringComparison.OrdinalIgnoreCase)) ||
           name.Equals("sourceMediaResourceId", StringComparison.OrdinalIgnoreCase) ||
           name.Equals("outputMediaResourceId", StringComparison.OrdinalIgnoreCase);

    private static HashSet<string> GetSuccessResponseSchemas(
        JsonElement operation,
        JsonElement schemas)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        foreach (var response in operation.GetProperty("responses").EnumerateObject()
                     .Where(response => response.Name.Length == 3 && response.Name[0] == '2'))
        {
            CollectReferencedSchemas(response.Value, schemas, result, []);
        }
        return result;
    }

    private static HashSet<string> GetReferencedSchemas(JsonElement element, JsonElement schemas)
    {
        var result = new HashSet<string>(StringComparer.Ordinal);
        CollectReferencedSchemas(element, schemas, result, []);
        return result;
    }

    private static void CollectReferencedSchemas(
        JsonElement element,
        JsonElement schemas,
        HashSet<string> result,
        HashSet<string> visited)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                CollectReferencedSchemas(item, schemas, result, visited);
            }
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return;
        }
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("$ref") && property.Value.ValueKind == JsonValueKind.String)
            {
                var reference = property.Value.GetString()!;
                var schemaName = reference[(reference.LastIndexOf('/') + 1)..];
                result.Add(schemaName);
                if (visited.Add(schemaName) && schemas.TryGetProperty(schemaName, out var schema))
                {
                    CollectReferencedSchemas(schema, schemas, result, visited);
                }
            }
            else
            {
                CollectReferencedSchemas(property.Value, schemas, result, visited);
            }
        }
    }

    private static bool ContainsProperty(JsonElement element, string propertyName)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            return element.EnumerateArray().Any(item => ContainsProperty(item, propertyName));
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            return false;
        }
        foreach (var property in element.EnumerateObject())
        {
            if (property.NameEquals("properties") &&
                property.Value.ValueKind == JsonValueKind.Object &&
                property.Value.TryGetProperty(propertyName, out _))
            {
                return true;
            }
            if (ContainsProperty(property.Value, propertyName))
            {
                return true;
            }
        }
        return false;
    }

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
        foreach (var composition in new[] { "anyOf", "oneOf" })
        {
            if (schema.TryGetProperty(composition, out var alternatives) &&
                alternatives.EnumerateArray().Any(value =>
                    GetSchemaTypes(value).Contains("null", StringComparer.Ordinal)))
            {
                return true;
            }
        }
        return false;
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
