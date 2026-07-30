namespace TinyLang.Entities;

/// <summary>
/// Represents an article body image association; cover media is stored separately on the article.
/// </summary>
public sealed class ArticleMediaResource
{
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public Guid MediaResourceId { get; set; }
    public MediaResource MediaResource { get; set; } = null!;
}
