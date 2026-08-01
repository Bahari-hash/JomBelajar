using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示由管理员维护、包含释义和发音的全局词条聚合根。
/// </summary>
public sealed class Word : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的词条草稿。
    /// </summary>
    public Word()
    {
        Id = Guid.NewGuid();
    }

    public required string LanguageTag { get; set; }
    public required string Headword { get; set; }
    public required string NormalizedHeadword { get; set; }
    public WordPublicationStatus Status { get; set; } = WordPublicationStatus.Draft;
    public Guid CreatedById { get; set; }
    public User? CreatedBy { get; set; }
    public Guid LastEditorId { get; set; }
    public User? LastEditor { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<WordSense> Senses { get; set; } = [];
    public ICollection<WordPronunciation> Pronunciations { get; set; } = [];
    public ICollection<UserWordProgress> UserProgress { get; set; } = [];
    public ICollection<WordStudySessionItem> StudySessionItems { get; set; } = [];
}
