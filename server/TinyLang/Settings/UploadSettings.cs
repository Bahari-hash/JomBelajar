using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述各类媒体上传的大小、扩展名和媒体类型限制。
/// </summary>
public sealed record UploadSettings
{
    public const string SectionName = "UploadSettings";

    [Range(1, int.MaxValue)]
    public int PictureMaxMB { get; init; }

    [Range(1, int.MaxValue)]
    public int AudioMaxMB { get; init; }

    [Range(1, int.MaxValue)]
    public int VideoMaxMB { get; init; }

    [Range(1, 20)]
    public int SubtitleMaxMB { get; init; } = 2;

    [Required, MinLength(1)]
    public required Dictionary<string, string[]> PictureAllowedTypes { get; init; }

    [Required, MinLength(1)]
    public required Dictionary<string, string[]> AudioAllowedTypes { get; init; }

    [Required, MinLength(1)]
    public required Dictionary<string, string[]> VideoAllowedTypes { get; init; }

    [Required, MinLength(1)]
    public Dictionary<string, string[]> SubtitleAllowedTypes { get; init; } = new()
    {
        [".vtt"] = ["text/vtt"]
    };
}
