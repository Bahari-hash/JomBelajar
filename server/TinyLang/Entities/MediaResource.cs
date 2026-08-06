using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示由用户上传并存储在对象存储中的媒体资源。
/// </summary>
public sealed class MediaResource : BaseAuditableEntity
{
    /// <summary>
    /// 创建一个处于待确认状态且已分配标识的媒体资源。
    /// </summary>
    public MediaResource()
    {
        Id = Guid.NewGuid();
    }

    public Guid UploaderId { get; set; }
    public User Uploader { get; set; } = null!;

    public required string ObjectName { get; set; }
    public string? StagingObjectName { get; set; }
    public required string OriginalName { get; set; }
    public ResourceModule Module { get; set; }
    public ResourceStatus Status { get; set; } = ResourceStatus.Pending;
    public long Size { get; set; }
    public required string Extension { get; set; }
    public required string ContentType { get; set; }
    public string? Url { get; set; }
    public DateTimeOffset? UploadExpiresAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public ICollection<Article> CoveredArticles { get; set; } = [];
    public ICollection<Video> CoveredVideos { get; set; } = [];
    public ICollection<ArticleMediaResource> ArticleMediaResources { get; set; } = [];
    public MultipartUploadSession? MultipartUploadSession { get; set; }
    public Video? SourceVideo { get; set; }
    public AudioClip? SourceAudioClip { get; set; }
}
