using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示单空填空题的一种可展示且可判分的标准写法。
/// </summary>
public sealed class FillBlankAcceptedAnswer : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的填空题可接受答案。
    /// </summary>
    public FillBlankAcceptedAnswer()
    {
        Id = Guid.NewGuid();
    }

    public Guid QuestionId { get; set; }
    public PaperQuestion? Question { get; set; }
    public required string Text { get; set; }
    public required string NormalizedText { get; set; }
    public int SortOrder { get; set; }
}
