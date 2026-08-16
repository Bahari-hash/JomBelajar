using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 可由文章、单词和听写等业务模块共享的独立音频资源。
/// </summary>
public sealed class AudioResource : BaseAuditableEntity
{
    private AudioResource()
    {
        Id = Guid.NewGuid();
    }

    /// <summary>
    /// 创建一个由管理员发起、等待源文件上传的音频资源。
    /// </summary>
    public static AudioResource Create(
        Guid adminId,
        string name,
        Guid sourceMediaResourceId)
    {
        var normalizedName = NormalizeName(name);

        return new AudioResource
        {
            CreatedById = adminId,
            LastEditorId = adminId,
            Name = name.Trim(),
            NormalizedName = normalizedName,
            SourceMediaResourceId = sourceMediaResourceId,
            Status = AudioResourceStatus.Uploading,
            ConcurrencyStamp = Guid.NewGuid()
        };
    }

    /// <summary>
    /// 生成用于大小写不敏感唯一约束的名称。
    /// </summary>
    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        return name.Trim().ToLowerInvariant();
    }

    public void Rename(Guid adminId, string name)
    {
        var displayName = name.Trim();
        Name = displayName;
        NormalizedName = NormalizeName(displayName);
        LastEditorId = adminId;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void ReplaceSource(Guid adminId, MediaResource source)
    {
        SourceMediaResourceId = source.Id;
        SourceMediaResource = source;
        Status = AudioResourceStatus.Uploading;
        LastFailureCode = null;
        LastEditorId = adminId;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public void Queue(Guid adminId)
    {
        Status = AudioResourceStatus.Queued;
        LastFailureCode = null;
        LastEditorId = adminId;
        ConcurrencyStamp = Guid.NewGuid();
    }

    public Guid CreatedById { get; private set; }
    public User CreatedBy { get; set; } = null!;
    public Guid LastEditorId { get; private set; }
    public User LastEditor { get; set; } = null!;

    /// <summary>
    /// 保留上传时的完整文件名，包括扩展名。
    /// </summary>
    public string Name { get; private set; } = null!;
    public string NormalizedName { get; private set; } = null!;
    public Guid SourceMediaResourceId { get; private set; }
    public MediaResource SourceMediaResource { get; set; } = null!;
    public AudioResourceStatus Status { get; set; } = AudioResourceStatus.Uploading;
    public double? DurationSeconds { get; set; }
    public int? SampleRate { get; set; }
    public int? Channels { get; set; }
    public string? ContainerFormat { get; set; }
    public string? SourceCodec { get; set; }
    public Guid? CurrentOutputVersion { get; set; }
    public string? OutputObjectName { get; set; }
    public string? LastFailureCode { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();

    public ICollection<AudioProcessingJob> ProcessingJobs { get; set; } = [];
}
