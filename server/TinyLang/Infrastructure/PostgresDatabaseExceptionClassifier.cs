using Microsoft.EntityFrameworkCore;
using Npgsql;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

/// <summary>
/// 通过 PostgreSQL SQLSTATE 和约束名称识别唯一键、外键约束冲突。
/// </summary>
public sealed class PostgresDatabaseExceptionClassifier : IDatabaseExceptionClassifier
{
    /// <inheritdoc />
    public bool IsUniqueConstraintViolation(
        DbUpdateException exception,
        params string[] constraintNames)
    {
        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.UniqueViolation,
                ConstraintName: { } constraintName
            })
        {
            return false;
        }

        return constraintNames.Contains(constraintName, StringComparer.OrdinalIgnoreCase);
    }

    /// <inheritdoc />
    public bool IsForeignKeyConstraintViolation(
        DbUpdateException exception,
        params string[] constraintNames)
    {
        if (exception.InnerException is not PostgresException
            {
                SqlState: PostgresErrorCodes.ForeignKeyViolation or
                    PostgresErrorCodes.RestrictViolation,
                ConstraintName: { } constraintName
            })
        {
            return false;
        }

        return constraintNames.Length == 0 ||
            constraintNames.Contains(constraintName, StringComparer.OrdinalIgnoreCase);
    }
}
