using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

public sealed class User : BaseAuditableEntity
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public UserRole Role { get; set; } = UserRole.User;

    public string? Nickname { get; set; }
    public string? AvatarUrl { get; set; }
    public string? Bio { get; set; }

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
}
