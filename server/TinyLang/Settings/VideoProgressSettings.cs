using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述播放位置容差和服务端完成状态判定规则。
/// </summary>
public sealed record VideoProgressSettings
{
    public const string SectionName = "VideoProgressSettings";

    [Range(0.5, 1.0)]
    public double CompletionRatio { get; init; } = 0.9;

    [Range(0, 300)]
    public int CompletionRemainingSeconds { get; init; } = 30;

    [Range(0, 30)]
    public int PositionToleranceSeconds { get; init; } = 3;
}
