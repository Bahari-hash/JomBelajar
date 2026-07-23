using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

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
    public int GlobalFallbackPermitLimit { get; init; }

    [Range(0, int.MaxValue)]
    public int GlobalFallbackQueueLimit { get; init; }
}
