using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 保存用户对试卷题目的错题记录及后续重做状态。
/// </summary>
public sealed class PaperWrongQuestion : BaseAuditableEntity
{
    public PaperWrongQuestion()
    {
        Id = Guid.NewGuid();
    }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public Guid QuestionId { get; set; }
    public PaperQuestion Question { get; set; } = null!;
    public PaperWrongQuestionStatus Status { get; set; } = PaperWrongQuestionStatus.Pending;
    public int WrongCount { get; set; }
    public int RedoCount { get; set; }
    public DateTimeOffset FirstWrongAt { get; set; }
    public DateTimeOffset LastWrongAt { get; set; }
    public DateTimeOffset? LastRedoAt { get; set; }
    public DateTimeOffset? MasteredAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
