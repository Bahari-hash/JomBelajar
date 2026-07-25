using System.Reflection;
using Microsoft.EntityFrameworkCore;
using TinyLang.Interfaces;

namespace TinyLang.Database;

/// <summary>
/// EF Core 数据库上下文，公开 TinyLang 聚合及关联实体集合。
/// </summary>
/// <param name="options">数据库上下文配置。</param>
public sealed class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : DbContext(options), IApplicationDbContext
{
    public DbSet<Entities.User> Users => Set<Entities.User>();
    public DbSet<Entities.RefreshToken> RefreshTokens => Set<Entities.RefreshToken>();
    public DbSet<Entities.MediaResource> MediaResources => Set<Entities.MediaResource>();
    public DbSet<Entities.MultipartUploadSession> MultipartUploadSessions => Set<Entities.MultipartUploadSession>();
    public DbSet<Entities.Article> Articles => Set<Entities.Article>();
    public DbSet<Entities.ArticleCategory> ArticleCategories => Set<Entities.ArticleCategory>();
    public DbSet<Entities.ArticleCategoryAssignment> ArticleCategoryAssignments => Set<Entities.ArticleCategoryAssignment>();
    public DbSet<Entities.ArticleMediaResource> ArticleMediaResources => Set<Entities.ArticleMediaResource>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
