using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
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
    public DbSet<Entities.Word> Words => Set<Entities.Word>();
    public DbSet<Entities.WordSense> WordSenses => Set<Entities.WordSense>();
    public DbSet<Entities.ExampleSentence> ExampleSentences => Set<Entities.ExampleSentence>();
    public DbSet<Entities.WordPronunciation> WordPronunciations => Set<Entities.WordPronunciation>();
    public DbSet<Entities.UserWordProgress> UserWordProgress => Set<Entities.UserWordProgress>();
    public DbSet<Entities.WordStudySession> WordStudySessions => Set<Entities.WordStudySession>();
    public DbSet<Entities.WordStudySessionItem> WordStudySessionItems => Set<Entities.WordStudySessionItem>();
    public DbSet<Entities.Paper> Papers => Set<Entities.Paper>();
    public DbSet<Entities.PaperQuestion> PaperQuestions => Set<Entities.PaperQuestion>();
    public DbSet<Entities.PaperQuestionOption> PaperQuestionOptions => Set<Entities.PaperQuestionOption>();
    public DbSet<Entities.FillBlankAcceptedAnswer> FillBlankAcceptedAnswers => Set<Entities.FillBlankAcceptedAnswer>();
    public DbSet<Entities.PaperAttempt> PaperAttempts => Set<Entities.PaperAttempt>();
    public DbSet<Entities.PaperAttemptAnswer> PaperAttemptAnswers => Set<Entities.PaperAttemptAnswer>();

    /// <inheritdoc />
    public async Task<IApplicationDbTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default)
    {
        if (!Database.IsRelational())
        {
            return NoOpApplicationDbTransaction.Instance;
        }

        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new RelationalApplicationDbTransaction(transaction);
    }

    /// <inheritdoc />
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());
    }

    /// <inheritdoc />
    public void ClearTrackedChanges() => ChangeTracker.Clear();

    /// <summary>
    /// 将 EF Core relational transaction 适配为应用事务契约。
    /// </summary>
    /// <param name="transaction">底层 EF Core transaction。</param>
    private sealed class RelationalApplicationDbTransaction(
        IDbContextTransaction transaction) : IApplicationDbTransaction
    {
        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
            => transaction.CommitAsync(cancellationToken);

        /// <inheritdoc />
        public ValueTask DisposeAsync() => transaction.DisposeAsync();
    }

    /// <summary>
    /// 为不支持事务的测试 provider 提供相同的调用边界。
    /// </summary>
    private sealed class NoOpApplicationDbTransaction : IApplicationDbTransaction
    {
        public static NoOpApplicationDbTransaction Instance { get; } = new();

        /// <inheritdoc />
        public Task CommitAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        /// <inheritdoc />
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
