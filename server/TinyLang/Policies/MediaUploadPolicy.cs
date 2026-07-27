using System.IO;
using Microsoft.Extensions.Options;
using TinyLang.Entities.Enums;
using TinyLang.Settings;

namespace TinyLang.Policies;

/// <summary>
/// 根据上传配置集中判断媒体模块、扩展名、大小、媒体类型和文件名是否合法。
/// </summary>
/// <param name="options">各媒体模块的上传限制配置。</param>
public sealed class MediaUploadPolicy(IOptions<UploadSettings> options)
{
    private readonly UploadSettings _settings = options.Value;

    /// <summary>
    /// 判断资源模块是否具有已定义的上传策略。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <returns>模块受上传流程支持时返回 <see langword="true"/>。</returns>
    public bool IsSupportedModule(ResourceModule module)
        => module is ResourceModule.Avatar or
            ResourceModule.ArticlePicture or
            ResourceModule.Audio or
            ResourceModule.CourseVideo or
            ResourceModule.VideoSubtitle;

    /// <summary>
    /// 判断资源模块是否仅供编辑者媒体上传使用。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <returns>模块属于文章图片、音频或课程视频时返回 <see langword="true"/>。</returns>
    public bool IsEditorModule(ResourceModule module)
        => module is ResourceModule.ArticlePicture or
            ResourceModule.Audio or
            ResourceModule.CourseVideo or
            ResourceModule.VideoSubtitle;

    /// <summary>
    /// 判断规范化扩展名是否在指定模块允许列表中。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <returns>扩展名受支持时返回 <see langword="true"/>。</returns>
    public bool IsExtensionAllowed(ResourceModule module, string? extension)
    {
        var normalized = NormalizeExtension(extension);
        return GetAllowedExtensions(module).Contains(
            normalized,
            StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// 判断文件大小是否大于零且未超过模块限制。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <param name="size">文件大小，单位为字节。</param>
    /// <returns>文件大小合法时返回 <see langword="true"/>。</returns>
    public bool IsSizeAllowed(ResourceModule module, long size)
        => size > 0 && size <= GetMaxSizeBytes(module);

    /// <summary>
    /// 判断媒体类型是否与模块和扩展名配置匹配。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <param name="extension">文件扩展名。</param>
    /// <param name="contentType">文件媒体类型。</param>
    /// <returns>扩展名和媒体类型组合受支持时返回 <see langword="true"/>。</returns>
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

    /// <summary>
    /// 获取指定资源模块允许的最大字节数。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <returns>最大字节数；不支持的模块返回零。</returns>
    public long GetMaxSizeBytes(ResourceModule module)
    {
        var megabytes = module switch
        {
            ResourceModule.Avatar or ResourceModule.ArticlePicture => _settings.PictureMaxMB,
            ResourceModule.Audio => _settings.AudioMaxMB,
            ResourceModule.CourseVideo => _settings.VideoMaxMB,
            ResourceModule.VideoSubtitle => _settings.SubtitleMaxMB,
            _ => 0
        };
        return (long)megabytes * 1024 * 1024;
    }

    /// <summary>
    /// 将扩展名转换为带前导点的小写形式。
    /// </summary>
    /// <param name="extension">待规范化扩展名。</param>
    /// <returns>规范化扩展名；空白输入返回空字符串。</returns>
    public static string NormalizeExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return string.Empty;
        }

        var normalized = extension.Trim().ToLowerInvariant();
        return normalized.StartsWith('.') ? normalized : $".{normalized}";
    }

    /// <summary>
    /// 去除媒体类型两端空白并转换为小写。
    /// </summary>
    /// <param name="contentType">待规范化媒体类型。</param>
    /// <returns>规范化媒体类型。</returns>
    public static string NormalizeContentType(string contentType)
        => contentType.Trim().ToLowerInvariant();

    /// <summary>
    /// 判断原始文件名是否为单个安全名称且不包含路径信息。
    /// </summary>
    /// <param name="originalName">客户端原始文件名。</param>
    /// <returns>文件名非空且不含目录穿越或路径分隔符时返回 <see langword="true"/>。</returns>
    public static bool IsSafeFileName(string? originalName)
        => !string.IsNullOrWhiteSpace(originalName) &&
            originalName is not "." and not ".." &&
            !originalName.Contains('/') &&
            !originalName.Contains('\\');

    /// <summary>
    /// 判断申报扩展名是否与原始文件名扩展名一致。
    /// </summary>
    /// <param name="originalName">客户端原始文件名。</param>
    /// <param name="extension">申报扩展名。</param>
    /// <returns>任一值为空或两个规范化扩展名一致时返回 <see langword="true"/>。</returns>
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

    /// <summary>
    /// 获取指定模块配置的扩展名集合。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <returns>允许的扩展名序列。</returns>
    private IEnumerable<string> GetAllowedExtensions(ResourceModule module)
        => GetAllowedTypes(module).Keys;

    /// <summary>
    /// 获取指定模块的扩展名到媒体类型映射。
    /// </summary>
    /// <param name="module">资源模块。</param>
    /// <returns>允许的扩展名及其媒体类型映射；不支持的模块返回空映射。</returns>
    private IReadOnlyDictionary<string, string[]> GetAllowedTypes(ResourceModule module)
        => module switch
        {
            ResourceModule.Avatar or ResourceModule.ArticlePicture
                => _settings.PictureAllowedTypes,
            ResourceModule.Audio => _settings.AudioAllowedTypes,
            ResourceModule.CourseVideo => _settings.VideoAllowedTypes,
            ResourceModule.VideoSubtitle => _settings.SubtitleAllowedTypes,
            _ => new Dictionary<string, string[]>()
        };
}
