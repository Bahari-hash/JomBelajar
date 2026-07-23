using TinyLang.Entities.Common;

namespace TinyLang.Entities;

public sealed class ArticleCategory : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ArticleCategoryAssignment> ArticleAssignments { get; set; } = [];
}
