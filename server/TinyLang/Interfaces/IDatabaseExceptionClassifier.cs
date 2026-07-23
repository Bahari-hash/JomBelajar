using Microsoft.EntityFrameworkCore;

namespace TinyLang.Interfaces;

public interface IDatabaseExceptionClassifier
{
    bool IsUniqueConstraintViolation(DbUpdateException exception, params string[] constraintNames);
}
