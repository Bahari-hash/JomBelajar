using Microsoft.EntityFrameworkCore;
using Npgsql;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class PostgresDatabaseExceptionClassifier : IDatabaseExceptionClassifier
{
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
}
