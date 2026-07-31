using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 定义创建和更新文章共享的可编辑字段。
/// </summary>
public abstract record ArticleUpsertRequest
{
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public required string ContentMarkdown { get; init; }
    public IReadOnlyCollection<Guid> CategoryIds { get; init; } = [];
    public Guid? CoverMediaResourceId { get; init; }
    public IReadOnlyCollection<Guid> BodyMediaResourceIds { get; init; } = [];
}

/// <summary>
/// 描述创建文章草稿的请求。
/// </summary>
public sealed record CreateArticleRequest : ArticleUpsertRequest;

/// <summary>
/// 描述更新现有文章草稿内容的请求。
/// </summary>
public sealed record UpdateArticleRequest : ArticleUpsertRequest
{
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// Describes Markdown submitted for a canonical server-side article preview.
/// </summary>
public sealed record ArticlePreviewRequest
{
    public required string ContentMarkdown { get; init; }
}

/// <summary>
/// 描述创建文章分类的请求。
/// </summary>
public sealed record CreateArticleCategoryRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
}

/// <summary>
/// 描述更新文章分类内容和启用状态的请求。
/// </summary>
public sealed record UpdateArticleCategoryRequest
{
    public required string Name { get; init; }
    public required string Slug { get; init; }
    public string? Description { get; init; }
    public bool IsActive { get; init; }
}

/// <summary>
/// 描述文章列表的分页、分类、关键词和状态筛选条件。
/// </summary>
public sealed record ArticleListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public Guid? CategoryId { get; init; }
    public string? Keyword { get; init; }
    public ArticleStatus? Status { get; init; }
}

/// <summary>
/// 描述公开文章分类列表的分页和关键词筛选条件。
/// </summary>
public record ArticleCategoryListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
}

/// <summary>
/// 描述管理员文章分类列表的筛选条件。
/// </summary>
public sealed record AdminArticleCategoryListRequest : ArticleCategoryListRequest
{
    public bool IncludeInactive { get; init; }
}

/// <summary>
/// 返回文章页面展示所需的用户摘要。
/// </summary>
/// <param name="Id">用户标识。</param>
/// <param name="Nickname">用户昵称。</param>
/// <param name="AvatarUrl">用户头像地址。</param>
public sealed record ArticleUserSummaryResponse(
    Guid Id,
    string? Nickname,
    string? AvatarUrl);

/// <summary>
/// 返回文章关联分类的摘要。
/// </summary>
/// <param name="Id">分类标识。</param>
/// <param name="Name">分类名称。</param>
/// <param name="Slug">分类 URL slug。</param>
public sealed record ArticleCategorySummaryResponse(
    Guid Id,
    string Name,
    string Slug);

/// <summary>
/// Returns a stable media identifier and URL mapping for article editing.
/// </summary>
/// <param name="Id">The media resource identifier.</param>
/// <param name="Url">The validated absolute public URL.</param>
public sealed record ArticleMediaReferenceResponse(Guid Id, string Url);

/// <summary>
/// Returns the canonical HTML generated for an article preview.
/// </summary>
/// <param name="ContentHtml">The canonical sanitized HTML.</param>
public sealed record ArticlePreviewResponse(string ContentHtml);

/// <summary>
/// Returns public article fields without Markdown source or internal editing metadata.
/// </summary>
/// <param name="Id">The article identifier.</param>
/// <param name="Title">The article title.</param>
/// <param name="Summary">The optional article summary.</param>
/// <param name="ContentHtml">The canonical sanitized HTML.</param>
/// <param name="Categories">The associated category summaries.</param>
/// <param name="Author">The author summary.</param>
/// <param name="PublishedAt">The publication timestamp.</param>
/// <param name="CoverUrl">The public cover URL.</param>
/// <param name="CreatedAt">The creation timestamp.</param>
/// <param name="UpdatedAt">The last update timestamp.</param>
public sealed record PublicArticleResponse(
    Guid Id,
    string Title,
    string? Summary,
    string ContentHtml,
    IReadOnlyCollection<ArticleCategorySummaryResponse> Categories,
    ArticleUserSummaryResponse Author,
    DateTimeOffset? PublishedAt,
    string? CoverUrl,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Returns the complete article editing contract, including Markdown, media mappings and concurrency state.
/// </summary>
/// <param name="Id">The article identifier.</param>
/// <param name="Title">The article title.</param>
/// <param name="Summary">The optional article summary.</param>
/// <param name="ContentMarkdown">The canonical editing source.</param>
/// <param name="ContentHtml">The canonical sanitized HTML derived from the Markdown source.</param>
/// <param name="Status">The current article status.</param>
/// <param name="Categories">The associated category summaries.</param>
/// <param name="Author">The author summary.</param>
/// <param name="LastEditor">The last administrator summary.</param>
/// <param name="PublishedAt">The publication timestamp.</param>
/// <param name="CoverMedia">The optional cover media mapping.</param>
/// <param name="BodyMedia">The body image mappings referenced by Markdown.</param>
/// <param name="ConcurrencyStamp">The optimistic concurrency token required by updates.</param>
/// <param name="CreatedAt">The creation timestamp.</param>
/// <param name="UpdatedAt">The last update timestamp.</param>
public sealed record AdminArticleResponse(
    Guid Id,
    string Title,
    string? Summary,
    string ContentMarkdown,
    string ContentHtml,
    ArticleStatus Status,
    IReadOnlyCollection<ArticleCategorySummaryResponse> Categories,
    ArticleUserSummaryResponse Author,
    ArticleUserSummaryResponse LastEditor,
    DateTimeOffset? PublishedAt,
    ArticleMediaReferenceResponse? CoverMedia,
    IReadOnlyCollection<ArticleMediaReferenceResponse> BodyMedia,
    Guid ConcurrencyStamp,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// Reports the result of explicitly clearing every article association from a category.
/// </summary>
/// <param name="CategoryId">The category identifier.</param>
/// <param name="RemovedArticleCount">The number of removed article associations.</param>
public sealed record ClearArticleCategoryResponse(Guid CategoryId, int RemovedArticleCount);

/// <summary>
/// 返回文章列表中单个条目的摘要信息。
/// </summary>
/// <param name="Id">文章标识。</param>
/// <param name="Title">文章标题。</param>
/// <param name="Summary">文章摘要。</param>
/// <param name="Status">文章当前状态。</param>
/// <param name="Categories">文章关联的分类。</param>
/// <param name="CoverUrl">封面媒体公开地址。</param>
/// <param name="Author">文章作者摘要。</param>
/// <param name="PublishedAt">文章发布时间。</param>
/// <param name="UpdatedAt">文章最后更新时间。</param>
public sealed record ArticleListItemResponse(
    Guid Id,
    string Title,
    string? Summary,
    ArticleStatus Status,
    IReadOnlyCollection<ArticleCategorySummaryResponse> Categories,
    string? CoverUrl,
    ArticleUserSummaryResponse Author,
    DateTimeOffset? PublishedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回文章分类的管理状态和关联文章数量。
/// </summary>
/// <param name="Id">分类标识。</param>
/// <param name="Name">分类名称。</param>
/// <param name="Slug">分类 URL slug。</param>
/// <param name="Description">分类描述。</param>
/// <param name="IsActive">分类是否可用于文章。</param>
/// <param name="ArticleCount">关联文章数量。</param>
public sealed record ArticleCategoryResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    bool IsActive,
    int ArticleCount,
    DateTimeOffset CreatedAt);

/// <summary>
/// 封装列表数据及其分页元数据。
/// </summary>
/// <typeparam name="T">列表元素类型。</typeparam>
/// <param name="Items">当前页元素。</param>
/// <param name="Page">当前页码。</param>
/// <param name="PageSize">每页元素数量。</param>
/// <param name="TotalCount">符合条件的元素总数。</param>
/// <param name="TotalPages">按当前页大小计算的总页数。</param>
public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

/// <summary>
/// 定义文章请求和业务规则共享的限制值。
/// </summary>
public static class ArticleConstraints
{
    public const int MaxContentLength = 1_000_000;
    public const int MaxCategoryCount = 10;
    public const int MaxBodyMediaCount = 100;
}
