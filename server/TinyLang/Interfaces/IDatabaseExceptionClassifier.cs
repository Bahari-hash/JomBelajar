using Microsoft.EntityFrameworkCore;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义将 provider-specific 数据库异常识别为稳定业务类别的契约。
/// </summary>
public interface IDatabaseExceptionClassifier
{
    /// <summary>
    /// 判断更新异常是否由指定数据库唯一约束之一触发。
    /// </summary>
    /// <param name="exception">EF Core 更新异常。</param>
    /// <param name="constraintNames">允许匹配的数据库约束名称。</param>
    /// <returns>异常对应任一指定唯一约束时返回 <see langword="true"/>。</returns>
    bool IsUniqueConstraintViolation(DbUpdateException exception, params string[] constraintNames);

    /// <summary>
    /// 判断更新异常是否由指定数据库外键约束之一触发。
    /// </summary>
    /// <param name="exception">EF Core 更新异常。</param>
    /// <param name="constraintNames">允许匹配的数据库约束名称。</param>
    /// <returns>未指定约束名时匹配任意外键冲突；否则仅匹配指定约束。</returns>
    bool IsForeignKeyConstraintViolation(
        DbUpdateException exception,
        params string[] constraintNames);
}
