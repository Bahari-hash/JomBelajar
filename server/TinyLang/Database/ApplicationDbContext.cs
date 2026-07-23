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
    public DbSet<Entities.Article> Articles => Set<Entities.Article>();
    public DbSet<Entities.ArticleCategory> ArticleCategories => Set<Entities.ArticleCategory>();
    public DbSet<Entities.ArticleCategoryAssignment> ArticleCategoryAssignments => Set<Entities.ArticleCategoryAssignment>();
    public DbSet<Entities.ArticleMediaResource> ArticleMediaResources => Set<Entities.ArticleMediaResource>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
