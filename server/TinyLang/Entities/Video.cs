using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示由上传源文件派生并可独立发布的视频业务实体。
/// </summary>
public sealed class Video : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识的视频草稿。
    /// </summary>
    public Video()
    {
        Id = Guid.NewGuid();
    }

    public Guid OwnerId { get; set; }
    public User Owner { get; set; } = null!;
    public Guid SourceMediaResourceId { get; set; }
    public MediaResource SourceMediaResource { get; set; } = null!;
    public required string Title { get; set; }
    public string? Description { get; set; }
    public required string OriginalLanguage { get; set; }
    public double? DurationSeconds { get; set; }
    public int? DisplayWidth { get; set; }
    public int? DisplayHeight { get; set; }
    public string? ContainerFormat { get; set; }
    public string? VideoCodec { get; set; }
    public string? AudioCodec { get; set; }
    public VideoProcessingStatus ProcessingStatus { get; set; } = VideoProcessingStatus.Queued;
    public VideoPublicationStatus PublicationStatus { get; set; } = VideoPublicationStatus.Draft;
    public Guid? CurrentOutputVersion { get; set; }
    public string? MasterPlaylistObjectName { get; set; }
    public string? PosterObjectName { get; set; }
    public string? LastFailureCode { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public ICollection<VideoProcessingJob> ProcessingJobs { get; set; } = [];
    public ICollection<VideoRendition> Renditions { get; set; } = [];
    public ICollection<VideoSubtitle> Subtitles { get; set; } = [];
    public ICollection<VideoCategoryAssignment> CategoryAssignments { get; set; } = [];
    public ICollection<UserVideoProgress> UserProgress { get; set; } = [];
}
