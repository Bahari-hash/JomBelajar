using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示由管理员维护并可发布给学习者阅读的文章。
/// </summary>
public sealed class Article : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public required string ContentMarkdown { get; set; }
    public required string ContentHtml { get; set; }
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public Guid LastEditorId { get; set; }
    public User LastEditor { get; set; } = null!;

    public Guid? PublishedById { get; set; }
    public User? PublishedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    public Guid? CoverMediaResourceId { get; set; }
    public MediaResource? CoverMediaResource { get; set; }

    public Guid? ReadingAudioResourceId { get; set; }
    public AudioResource? ReadingAudioResource { get; set; }

    public ICollection<ArticleMediaResource> MediaResources { get; set; } = [];
    public ICollection<ArticleCategoryAssignment> CategoryAssignments { get; set; } = [];
}
