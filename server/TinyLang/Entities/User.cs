using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示 TinyLang 用户及其账户、资料和权限状态。
/// </summary>
public sealed class User : BaseAuditableEntity
{
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.User;

    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }
    public int DailyWordStudyCount { get; set; } = 20;
    public int DailyWordReviewCount { get; set; } = 50;

    public double DesiredRetention { get; set; } = 0.9;

    public bool IsBanned { get; set; }
    public DateTimeOffset? BannedAt { get; set; }
    public string? BannedReason { get; set; }

    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public int TokenVersion { get; set; }

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];
    public ICollection<MediaResource> MediaResources { get; set; } = [];
    public ICollection<Article> AuthoredArticles { get; set; } = [];
    public ICollection<Article> EditedArticles { get; set; } = [];
    public ICollection<Article> PublishedArticles { get; set; } = [];
    public ICollection<Video> CreatedVideos { get; set; } = [];
    public ICollection<Video> EditedVideos { get; set; } = [];
    public ICollection<AudioResource> CreatedAudioResources { get; set; } = [];
    public ICollection<AudioResource> EditedAudioResources { get; set; } = [];
    public ICollection<UserVideoProgress> VideoProgress { get; set; } = [];
    public ICollection<UserWordProgress> WordProgress { get; set; } = [];
    public ICollection<UserWordFavorite> WordFavorites { get; set; } = [];
    public ICollection<WordStudySession> WordStudySessions { get; set; } = [];
    public ICollection<WordStudyActivity> WordStudyActivities { get; set; } = [];
    public ICollection<WordStudyCheckIn> WordStudyCheckIns { get; set; } = [];
    public ICollection<Paper> CreatedPapers { get; set; } = [];
    public ICollection<Paper> EditedPapers { get; set; } = [];
    public ICollection<PaperAttempt> PaperAttempts { get; set; } = [];
    public ICollection<PaperWrongQuestion> PaperWrongQuestions { get; set; } = [];
}
