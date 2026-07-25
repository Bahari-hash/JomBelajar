using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示用户的一次可撤销 refresh token 会话。
/// </summary>
public sealed class RefreshToken : BaseAuditableEntity
{
    public required string TokenHash { get; set; }

    public Guid UserId { get; set; }
    public User User { get; set; } = null!;
    public int TokenVersion { get; set; }

    public string? ClientIp { get; set; }
    public string? DeviceInfo { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? LoginAt { get; set; }
    public DateTimeOffset? LastUsedAt { get; set; }
    public long UsageCount { get; set; }

    public bool IsRevoked { get; set; }
    public DateTimeOffset? RevokedAt { get; set; }
}
