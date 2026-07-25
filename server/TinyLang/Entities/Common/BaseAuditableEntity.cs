namespace TinyLang.Entities.Common;

/// <summary>
/// 为实体提供创建时间和最后更新时间审计信息。
/// </summary>
public abstract class BaseAuditableEntity : BaseEntity
{
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
