using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TinyLang.Interfaces;

namespace TinyLang.Database;

public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Entities.User> Users => Set<Entities.User>();
    public DbSet<Entities.RefreshToken> RefreshTokens => Set<Entities.RefreshToken>();
    public DbSet<Entities.MediaResource> MediaResources => Set<Entities.MediaResource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
