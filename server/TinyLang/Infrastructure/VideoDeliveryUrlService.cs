using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用对象存储公共直链生成媒体地址。
/// </summary>
public sealed class VideoDeliveryUrlService : IVideoDeliveryUrlService
{
    private readonly IObjectStorageService _objectStorage;

    /// <summary>
    /// 使用对象存储创建地址服务。
    /// </summary>
    public VideoDeliveryUrlService(IObjectStorageService objectStorage)
    {
        _objectStorage = objectStorage;
    }

    /// <inheritdoc />
    public VideoDeliveryUrl CreateUrl(string objectName, string protectedPrefix)
    {
        var normalizedObjectName = NormalizeObjectPath(objectName, allowTrailingSlash: false);
        var normalizedPrefix = NormalizeObjectPath(protectedPrefix, allowTrailingSlash: true);
        if (!normalizedObjectName.StartsWith(normalizedPrefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "The object name is outside the protected prefix.",
                nameof(objectName));
        }

        return new VideoDeliveryUrl(
            _objectStorage.GetPublicUrl(normalizedObjectName),
            ExpiresAt: null);
    }

    /// <summary>
    /// 规范化服务端对象路径并拒绝绝对路径、空段和目录穿越。
    /// </summary>
    private static string NormalizeObjectPath(string path, bool allowTrailingSlash)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            path.StartsWith('/') ||
            path.Contains('\\'))
        {
            throw new ArgumentException("Invalid object path.", nameof(path));
        }
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0 || segments.Any(segment => segment is "." or ".."))
        {
            throw new ArgumentException("Invalid object path.", nameof(path));
        }
        var normalized = string.Join('/', segments);
        return allowTrailingSlash ? $"{normalized}/" : normalized;
    }

    /// <summary>
}
