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
    public DbSet<Entities.Video> Videos => Set<Entities.Video>();
    public DbSet<Entities.VideoProcessingJob> VideoProcessingJobs => Set<Entities.VideoProcessingJob>();
    public DbSet<Entities.VideoRendition> VideoRenditions => Set<Entities.VideoRendition>();
    public DbSet<Entities.VideoSubtitle> VideoSubtitles => Set<Entities.VideoSubtitle>();
    public DbSet<Entities.VideoCategory> VideoCategories => Set<Entities.VideoCategory>();
    public DbSet<Entities.VideoCategoryAssignment> VideoCategoryAssignments => Set<Entities.VideoCategoryAssignment>();
    public DbSet<Entities.UserVideoProgress> UserVideoProgress => Set<Entities.UserVideoProgress>();
    public DbSet<Entities.AudioClip> AudioClips => Set<Entities.AudioClip>();
    public DbSet<Entities.AudioProcessingJob> AudioProcessingJobs => Set<Entities.AudioProcessingJob>();

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }
}
