namespace TinyLang.Entities;

/// <summary>
/// 表示文章与分类之间的多对多关联。
/// </summary>
public sealed class ArticleCategoryAssignment
{
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public Guid ArticleCategoryId { get; set; }
    public ArticleCategory ArticleCategory { get; set; } = null!;
}
