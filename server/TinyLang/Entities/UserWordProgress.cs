using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 保存一个用户对一个全局词条已经完成的累计背诵结果。
/// </summary>
public sealed class UserWordProgress : BaseAuditableEntity
{
    /// <summary>
    /// 创建具有独立标识的用户词条进度。
    /// </summary>
    public UserWordProgress()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User? User { get; set; }
    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public int ReviewCount { get; set; }
    public int RememberedCount { get; set; }
    public int ForgottenCount { get; set; }
    public WordStudyResult LastResult { get; set; }
    public DateTimeOffset FirstStudiedAt { get; set; }
    public DateTimeOffset LastStudiedAt { get; set; }
}
