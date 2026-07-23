namespace TinyLang.Entities;

public sealed class ArticleMediaResource
{
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public Guid MediaResourceId { get; set; }
    public MediaResource MediaResource { get; set; } = null!;
}
