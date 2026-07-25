namespace TinyLang.Entities;

/// <summary>
/// 表示文章与其使用的媒体资源之间的关联。
/// </summary>
public sealed class ArticleMediaResource
{
    public Guid ArticleId { get; set; }
    public Article Article { get; set; } = null!;

    public Guid MediaResourceId { get; set; }
    public MediaResource MediaResource { get; set; } = null!;
}
