using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

public sealed record UploadSettings
{
    public const string SectionName = "UploadSettings";

    [Range(1, int.MaxValue)]
    public int PictureMaxMB { get; init; }

    [Range(1, int.MaxValue)]
    public int AudioMaxMB { get; init; }

    [Range(1, int.MaxValue)]
    public int VideoMaxMB { get; init; }

    [Required, MinLength(1)]
    public required Dictionary<string, string[]> PictureAllowedTypes { get; init; }

    [Required, MinLength(1)]
    public required Dictionary<string, string[]> AudioAllowedTypes { get; init; }

    [Required, MinLength(1)]
    public required Dictionary<string, string[]> VideoAllowedTypes { get; init; }
}
