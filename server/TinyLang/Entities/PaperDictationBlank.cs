using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示听写题中按顺序填写的一个空及其唯一标准答案。
/// </summary>
public sealed class PaperDictationBlank : BaseEntity
{
    public PaperDictationBlank()
    {
        Id = Guid.NewGuid();
    }

    public Guid QuestionId { get; set; }
    public PaperQuestion? Question { get; set; }
    public required string Answer { get; set; }
    public required string NormalizedAnswer { get; set; }
    public int SortOrder { get; set; }
}
