using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示用于组织和筛选在线试卷的可管理分类。
/// </summary>
public sealed class PaperCategory : BaseAuditableEntity
{
    public required string Name { get; set; }
    public required string Slug { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<PaperCategoryAssignment> PaperAssignments { get; set; } = [];
}
