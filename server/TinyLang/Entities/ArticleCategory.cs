using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示用于组织和筛选文章的可管理分类。
/// </summary>
public sealed class ArticleCategory : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ArticleCategoryAssignment> ArticleAssignments { get; set; } = [];
}
