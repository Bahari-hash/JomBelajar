using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示单选题内一个有序选项及其标准答案标记。
/// </summary>
public sealed class PaperQuestionOption : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的单选题选项。
    /// </summary>
    public PaperQuestionOption()
    {
        Id = Guid.NewGuid();
    }

    public Guid QuestionId { get; set; }
    public PaperQuestion? Question { get; set; }
    public required string Text { get; set; }
    public bool IsCorrect { get; set; }
    public int SortOrder { get; set; }
    public ICollection<PaperAttemptAnswer> SelectedByAnswers { get; set; } = [];
}
