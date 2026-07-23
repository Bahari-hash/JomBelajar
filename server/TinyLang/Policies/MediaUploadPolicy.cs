using System.IO;
using Microsoft.Extensions.Options;
using TinyLang.Entities.Enums;
using TinyLang.Settings;

namespace TinyLang.Policies;

public sealed class MediaUploadPolicy(IOptions<UploadSettings> options)
{
    private readonly UploadSettings _settings = options.Value;

    public bool IsSupportedModule(ResourceModule module)
        => module is ResourceModule.Avatar or
            ResourceModule.ArticlePicture or
            ResourceModule.Audio or
            ResourceModule.CourseVideo;

    public bool IsEditorModule(ResourceModule module)
        => module is ResourceModule.ArticlePicture or
            ResourceModule.Audio or
            ResourceModule.CourseVideo;

    public bool IsExtensionAllowed(ResourceModule module, string? extension)
    {
        var normalized = NormalizeExtension(extension);
        return GetAllowedExtensions(module).Contains(
            normalized,
            StringComparer.OrdinalIgnoreCase);
    }

    public bool IsSizeAllowed(ResourceModule module, long size)
        => size > 0 && size <= GetMaxSizeBytes(module);

    public bool IsContentTypeAllowed(
        ResourceModule module,
        string? extension,
        string? contentType)
    {
        if (string.IsNullOrWhiteSpace(contentType) ||
            !IsExtensionAllowed(module, extension))
        {
            return false;
        }

        var normalizedExtension = NormalizeExtension(extension);
        var mapping = GetAllowedTypes(module).FirstOrDefault(pair =>
            string.Equals(
                pair.Key,
                normalizedExtension,
                StringComparison.OrdinalIgnoreCase));
        return mapping.Value is not null &&
            mapping.Value.Contains(
                NormalizeContentType(contentType),
                StringComparer.OrdinalIgnoreCase);
    }

    public long GetMaxSizeBytes(ResourceModule module)
    {
        var megabytes = module switch
        {
            ResourceModule.Avatar or ResourceModule.ArticlePicture => _settings.PictureMaxMB,
            ResourceModule.Audio => _settings.AudioMaxMB,
            ResourceModule.CourseVideo => _settings.VideoMaxMB,
            _ => 0
        };
        return (long)megabytes * 1024 * 1024;
    }

    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }

    public static string NormalizeContentType(string contentType)
        => contentType.Trim().ToLowerInvariant();

    public static bool IsSafeFileName(string? originalName)
        => !string.IsNullOrWhiteSpace(originalName) &&
            originalName is not "." and not ".." &&
            !originalName.Contains('/') &&
            !originalName.Contains('\\');

    public static bool ExtensionMatchesName(string? originalName, string? extension)
    {
        if (string.IsNullOrWhiteSpace(originalName) ||
            string.IsNullOrWhiteSpace(extension))
        {
            return true;
        }

        return string.Equals(
            NormalizeExtension(Path.GetExtension(originalName)),
            NormalizeExtension(extension),
            StringComparison.OrdinalIgnoreCase);
    }

    private IEnumerable<string> GetAllowedExtensions(ResourceModule module)
        => GetAllowedTypes(module).Keys;

    private IReadOnlyDictionary<string, string[]> GetAllowedTypes(ResourceModule module)
        => module switch
        {
            ResourceModule.Avatar or ResourceModule.ArticlePicture
                => _settings.PictureAllowedTypes,
            ResourceModule.Audio => _settings.AudioAllowedTypes,
            ResourceModule.CourseVideo => _settings.VideoAllowedTypes,
            _ => new Dictionary<string, string[]>()
        };
}
