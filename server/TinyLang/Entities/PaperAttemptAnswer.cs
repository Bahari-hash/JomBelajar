using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 保存一次测验中一道题的用户答案和提交后的判分结果。
/// </summary>
public sealed class PaperAttemptAnswer : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识和并发标识的题目答案。
    /// </summary>
    public PaperAttemptAnswer()
    {
        Id = Guid.NewGuid();
    }

    public Guid AttemptId { get; set; }
    public PaperAttempt? Attempt { get; set; }
    public Guid QuestionId { get; set; }
    public PaperQuestion? Question { get; set; }
    public Guid? SelectedOptionId { get; set; }
    public PaperQuestionOption? SelectedOption { get; set; }
    public bool? BooleanAnswer { get; set; }
    public string? TextAnswer { get; set; }
    public string? NormalizedTextAnswer { get; set; }
    public string[]? TextAnswers { get; set; }
    public bool IsAnswered { get; set; }
    public bool? IsCorrect { get; set; }
    public int? AwardedPoints { get; set; }
    public DateTimeOffset? SavedAt { get; set; }
    public Guid ConcurrencyStamp { get; set; } = Guid.NewGuid();
}
