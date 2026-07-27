namespace TinyLang.Interfaces;

using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;

/// <summary>
/// 定义业务服务访问和持久化应用实体所需的数据库上下文契约。
/// </summary>
public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<MediaResource> MediaResources { get; }
    DbSet<MultipartUploadSession> MultipartUploadSessions { get; }
    DbSet<Article> Articles { get; }
    DbSet<ArticleCategory> ArticleCategories { get; }
    DbSet<ArticleCategoryAssignment> ArticleCategoryAssignments { get; }
    DbSet<ArticleMediaResource> ArticleMediaResources { get; }
    DbSet<Video> Videos { get; }
    DbSet<VideoProcessingJob> VideoProcessingJobs { get; }
    DbSet<VideoRendition> VideoRenditions { get; }
    DbSet<VideoSubtitle> VideoSubtitles { get; }
    DbSet<UserVideoProgress> UserVideoProgress { get; }
    /// <summary>
    /// 将当前上下文中跟踪的变更持久化到底层数据库。
    /// </summary>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>写入数据库的状态项数量。</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
