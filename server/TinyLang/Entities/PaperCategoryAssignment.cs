namespace TinyLang.Entities;

/// <summary>
/// 表示试卷与分类之间的多对多关联。
/// </summary>
public sealed class PaperCategoryAssignment
{
    public Guid PaperId { get; set; }
    public Paper Paper { get; set; } = null!;

    public Guid PaperCategoryId { get; set; }
    public PaperCategory PaperCategory { get; set; } = null!;
}
