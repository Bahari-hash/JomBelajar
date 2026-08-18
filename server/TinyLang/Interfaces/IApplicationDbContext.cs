namespace TinyLang.Interfaces;

using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;
using TinyLang.Entities.Enums;

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
    DbSet<VideoCategory> VideoCategories { get; }
    DbSet<VideoCategoryAssignment> VideoCategoryAssignments { get; }
    DbSet<UserVideoProgress> UserVideoProgress { get; }
    DbSet<AudioResource> AudioResources { get; }
    DbSet<AudioProcessingJob> AudioProcessingJobs { get; }
    DbSet<Word> Words { get; }
    DbSet<WordSense> WordSenses { get; }
    DbSet<ExampleSentence> ExampleSentences { get; }
    DbSet<UserWordProgress> UserWordProgress { get; }
    DbSet<UserWordFavorite> UserWordFavorites { get; }
    DbSet<WordStudySession> WordStudySessions { get; }
    DbSet<WordStudySessionItem> WordStudySessionItems { get; }
    DbSet<WordStudyActivity> WordStudyActivities { get; }
    DbSet<WordStudyCheckIn> WordStudyCheckIns { get; }
    DbSet<Paper> Papers { get; }
    DbSet<PaperQuestion> PaperQuestions { get; }
    DbSet<PaperQuestionOption> PaperQuestionOptions { get; }
    DbSet<FillBlankAcceptedAnswer> FillBlankAcceptedAnswers { get; }
    DbSet<PaperAttempt> PaperAttempts { get; }
    DbSet<PaperAttemptAnswer> PaperAttemptAnswers { get; }
    /// <summary>
    /// 开始一个用于多次保存同一业务变更的数据库事务。
    /// </summary>
    /// <param name="cancellationToken">用于取消事务创建的令牌。</param>
    /// <returns>可提交和异步释放的事务契约。</returns>
    Task<IApplicationDbTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 将当前上下文中跟踪的变更持久化到底层数据库。
    /// </summary>
    /// <param name="cancellationToken">用于取消数据库操作的令牌。</param>
    /// <returns>写入数据库的状态项数量。</returns>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
    /// <summary>
    /// 清除失败事务遗留的 tracked 状态，以便重新读取数据库最终结果。
    /// </summary>
    void ClearTrackedChanges();
    /// <summary>
    /// 在当前事务内为指定上传者取得串行化媒体激活检查的 PostgreSQL advisory lock。
    /// </summary>
    Task AcquireUploaderActivationLockAsync(
        Guid uploaderId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 在当前事务内串行化同一音频资源的上传确认。
    /// </summary>
    Task AcquireAudioResourceLockAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 在当前事务内串行化同一用户、同一类型的单词学习会话创建。
    /// </summary>
    Task AcquireWordStudySessionLockAsync(
        Guid userId,
        WordStudySessionType sessionType,
        CancellationToken cancellationToken = default);
}
