using System.Text.Json.Serialization;
using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

public abstract record ArticleUpsertRequest
{
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public required string ContentHtml { get; init; }
    public Guid? CategoryId { get; init; }
    public Guid? CoverMediaResourceId { get; init; }
    public IReadOnlyCollection<Guid> MediaResourceIds { get; init; } = [];
}

public sealed record CreateArticleRequest : ArticleUpsertRequest;

public sealed record UpdateArticleRequest : ArticleUpsertRequest;

public sealed record CreateArticleCategoryRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
}

public sealed record UpdateArticleCategoryRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

public sealed record ArticleListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? CategoryId { get; init; }
    public string? Keyword { get; init; }
    public ArticleStatus? Status { get; init; }
}

public record ArticleCategoryListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
}

public sealed record AdminArticleCategoryListRequest : ArticleCategoryListRequest
{
    public bool IncludeInactive { get; init; }
}

public sealed record ArticleUserSummaryResponse(
    Guid Id,
    string? Nickname,
    string? AvatarUrl);

public sealed record ArticleCategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug);

public sealed record ArticleResponse(
    Guid Id,
    string Title,
    string? Summary,
    string ContentHtml,
    ArticleStatus Status,
    ArticleCategorySummaryResponse? Category,
    ArticleUserSummaryResponse Author,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ArticleUserSummaryResponse? LastEditor,
    DateTimeOffset? PublishedAt,
    string? CoverUrl,
    IReadOnlyCollection<Guid> MediaResourceIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record ArticleListItemResponse(
    Guid Id,
    string Title,
    string? Summary,
    ArticleStatus Status,
    ArticleCategorySummaryResponse? Category,
    string? CoverUrl,
    ArticleUserSummaryResponse Author,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt);

public sealed record ArticleCategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int ArticleCount);

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);
