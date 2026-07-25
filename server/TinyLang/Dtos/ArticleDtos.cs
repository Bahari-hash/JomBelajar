using System.Text.Json.Serialization;
using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

/// <summary>
/// 定义创建和更新文章共享的可编辑字段。
/// </summary>
public abstract record ArticleUpsertRequest
{
    public required string Title { get; init; }
    public string? Summary { get; init; }
    public required string ContentHtml { get; init; }
    public IReadOnlyCollection<Guid> CategoryIds { get; init; } = [];
    public Guid? CoverMediaResourceId { get; init; }
    public IReadOnlyCollection<Guid> MediaResourceIds { get; init; } = [];
}

/// <summary>
/// 描述创建文章草稿的请求。
/// </summary>
public sealed record CreateArticleRequest : ArticleUpsertRequest;

/// <summary>
/// 描述更新现有文章草稿内容的请求。
/// </summary>
public sealed record UpdateArticleRequest : ArticleUpsertRequest;

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
/// 返回文章详情及其作者、分类和媒体关联信息。
/// </summary>
/// <param name="Id">文章标识。</param>
/// <param name="Title">文章标题。</param>
/// <param name="Summary">文章摘要。</param>
/// <param name="ContentHtml">清理后的文章 HTML 正文。</param>
/// <param name="Status">文章当前状态。</param>
/// <param name="Categories">文章关联的分类。</param>
/// <param name="Author">文章作者摘要。</param>
/// <param name="LastEditor">最后编辑者摘要；公开响应中可省略。</param>
/// <param name="PublishedAt">文章发布时间。</param>
/// <param name="CoverUrl">封面媒体公开地址。</param>
/// <param name="MediaResourceIds">文章关联的媒体资源标识。</param>
/// <param name="CreatedAt">文章创建时间。</param>
/// <param name="UpdatedAt">文章最后更新时间。</param>
public sealed record ArticleResponse(
    Guid Id,
    string Title,
    string? Summary,
    string ContentHtml,
    ArticleStatus Status,
    IReadOnlyCollection<ArticleCategorySummaryResponse> Categories,
    ArticleUserSummaryResponse Author,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ArticleUserSummaryResponse? LastEditor,
    DateTimeOffset? PublishedAt,
    string? CoverUrl,
    IReadOnlyCollection<Guid> MediaResourceIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

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
    int ArticleCount);

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
    public const int MaxCategoryCount = 10;
}
