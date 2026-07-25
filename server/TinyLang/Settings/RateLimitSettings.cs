using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述验证码、上传预签名和全局请求的限流参数。
/// </summary>
public sealed record RateLimitSettings
{
    public const string SectionName = "RateLimitSettings";

    [Range(1, int.MaxValue)]
    public int StrictCodePermitLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int StrictCodeWindowSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadPresignTokenLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadPresignTokensPerPeriod { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadPresignReplenishmentPeriodSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadCommandTokenLimit { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadCommandTokensPerPeriod { get; init; }

    [Range(1, int.MaxValue)]
    public int UploadCommandReplenishmentPeriodSeconds { get; init; }

    [Range(1, int.MaxValue)]
    public int GlobalFallbackPermitLimit { get; init; }

    [Range(0, int.MaxValue)]
    public int GlobalFallbackQueueLimit { get; init; }
}
