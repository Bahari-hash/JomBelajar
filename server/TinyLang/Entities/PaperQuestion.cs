using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示试卷内一道具有固定题型、分值和顺序的题目。
/// </summary>
public sealed class PaperQuestion : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的试卷题目。
    /// </summary>
    public PaperQuestion()
    {
        Id = Guid.NewGuid();
    }

    public Guid PaperId { get; set; }
    public Paper? Paper { get; set; }
    public PaperQuestionType Type { get; set; }
    public required string Prompt { get; set; }
    public string? Explanation { get; set; }
    public int Points { get; set; }
    public int SortOrder { get; set; }
    public bool? CorrectBoolean { get; set; }
    public bool FillBlankCaseSensitive { get; set; }
    public Guid? AudioResourceId { get; set; }
    public AudioResource? AudioResource { get; set; }
    public ICollection<PaperQuestionOption> Options { get; set; } = [];
    public ICollection<FillBlankAcceptedAnswer> AcceptedAnswers { get; set; } = [];
    public ICollection<PaperDictationBlank> DictationBlanks { get; set; } = [];
    public ICollection<PaperAttemptAnswer> AttemptAnswers { get; set; } = [];
    public ICollection<PaperWrongQuestion> WrongQuestions { get; set; } = [];
}
