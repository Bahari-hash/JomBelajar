namespace TinyLang.Entities;

public sealed class ArticleCategoryAssignment
{
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public Guid ArticleCategoryId { get; set; }
    public ArticleCategory ArticleCategory { get; set; } = null!;
}
