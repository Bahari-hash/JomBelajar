using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

public sealed class Article : BaseAuditableEntity
{
    public required string Title { get; set; }
    public string? Summary { get; set; }
    public required string ContentHtml { get; set; }
    public ArticleStatus Status { get; set; } = ArticleStatus.Draft;

    public Guid AuthorId { get; set; }
    public User Author { get; set; } = null!;

    public Guid LastEditorId { get; set; }
    public User LastEditor { get; set; } = null!;

    public Guid? PublishedById { get; set; }
    public User? PublishedBy { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }

    public Guid? CoverMediaResourceId { get; set; }
    public MediaResource? CoverMediaResource { get; set; }

    public ICollection<ArticleMediaResource> MediaResources { get; set; } = [];
    public ICollection<ArticleCategoryAssignment> CategoryAssignments { get; set; } = [];
}
