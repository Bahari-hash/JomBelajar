using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示由上传源文件派生、可供多个未来业务实体复用的音频资产。
/// </summary>
public sealed class AudioClip : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识的音频草稿。
    /// </summary>
    public AudioClip()
    {
        Id = Guid.NewGuid();
    }

    public Guid CreatedById { get; set; }
    public User CreatedBy { get; set; } = null!;
    public Guid LastEditorId { get; set; }
    public User LastEditor { get; set; } = null!;
    public Guid SourceMediaResourceId { get; set; }
    public required MediaResource SourceMediaResource { get; set; }
    public required string Title { get; set; }
    public string? Description { get; set; }
    public AudioClipKind Kind { get; set; }
    public double? DurationSeconds { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public string? ContainerFormat { get; set; }
    public string? SourceCodec { get; set; }
    public AudioProcessingStatus ProcessingStatus { get; set; } = AudioProcessingStatus.Queued;
    public AudioPublicationStatus PublicationStatus { get; set; } = AudioPublicationStatus.Draft;
    public Guid? CurrentOutputVersion { get; set; }
    public string? OutputObjectName { get; set; }
    public string? LastFailureCode { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public ICollection<AudioProcessingJob> ProcessingJobs { get; set; } = [];
}
