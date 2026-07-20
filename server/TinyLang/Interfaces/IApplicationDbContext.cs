namespace TinyLang.Interfaces;

using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
