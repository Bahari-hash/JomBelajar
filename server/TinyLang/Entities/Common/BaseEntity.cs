namespace TinyLang.Entities.Common;

/// <summary>
/// 为持久化实体提供全局唯一标识。
/// </summary>
public abstract class BaseEntity
{
    public Guid Id { get; protected set; }
}
