using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

public sealed class MediaResource : BaseAuditableEntity
{
    public MediaResource()
    {
        Id = Guid.NewGuid();
    }

    public Guid UploaderId { get; set; }
    public User Uploader { get; set; } = null!;

    public required string ObjectName { get; set; }
    public required string OriginalName { get; set; }
    public ResourceModule Module { get; set; }
    public ResourceStatus Status { get; set; } = ResourceStatus.Pending;
    public long Size { get; set; }
    public required string Extension { get; set; }
    public required string ContentType { get; set; }
    public string? Url { get; set; }

    public ICollection<Article> CoveredArticles { get; set; } = [];
    public ICollection<ArticleMediaResource> ArticleMediaResources { get; set; } = [];
}
