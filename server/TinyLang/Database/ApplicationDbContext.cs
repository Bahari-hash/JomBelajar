using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TinyLang.Interfaces;

namespace TinyLang.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<TinyLang.Entities.User> Users => Set<TinyLang.Entities.User>();
    public DbSet<TinyLang.Entities.RefreshToken> RefreshTokens => Set<TinyLang.Entities.RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
