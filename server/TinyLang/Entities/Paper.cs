using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示由管理员维护并发布给学习用户的试卷聚合根。
/// </summary>
public sealed class Paper : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的试卷草稿。
    /// </summary>
    public Paper()
    {
        Id = Guid.NewGuid();
    }

    public required string Title { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    public required string LanguageTag { get; set; }
    public PaperPublicationStatus Status { get; set; } =
        PaperPublicationStatus.Draft;
    public int PassingScore { get; set; }
    public int TotalScore { get; set; }
    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public Guid LastEditorId { get; set; }
    public User? LastEditor { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<PaperQuestion> Questions { get; set; } = [];
    public ICollection<PaperAttempt> Attempts { get; set; } = [];
}
