using TinyLang.Entities.Common;
namespace TinyLang.Entities;

/// <summary>
/// 表示由管理员维护、包含释义和可选共享音频的全局词条聚合根。
/// </summary>
public sealed class Word : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的词条。
    /// </summary>
    public Word()
    {
        Id = Guid.NewGuid();
    }

    public required string Headword { get; set; }
    public required string NormalizedHeadword { get; set; }
    public long StudyOrder { get; set; }
    public bool IsDeleted { get; set; }
    public Guid? AudioResourceId { get; set; }
    public AudioResource? AudioResource { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
    public ICollection<WordSense> Senses { get; set; } = [];
    public ICollection<UserWordProgress> UserProgress { get; set; } = [];
    public ICollection<UserWordFavorite> Favorites { get; set; } = [];
    public ICollection<WordStudySessionItem> StudySessionItems { get; set; } = [];
    public ICollection<WordStudyActivity> WordStudyActivities { get; set; } = [];
}
