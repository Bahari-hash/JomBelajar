using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用对象存储公共直链或供应商无关 HMAC-SHA256 auth path 生成媒体地址。
/// </summary>
public sealed class VideoDeliveryUrlService : IVideoDeliveryUrlService
{
    private readonly IObjectStorageService _objectStorage;
    private readonly VideoDeliverySettings _settings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 使用对象存储、delivery 配置和可替换时间源创建地址服务。
    /// </summary>
    public VideoDeliveryUrlService(
        IObjectStorageService objectStorage,
        IOptions<VideoDeliverySettings> options,
        TimeProvider timeProvider)
    {
        _objectStorage = objectStorage;
        _settings = options.Value;
        _timeProvider = timeProvider;
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

        if (_settings.Mode == "DirectObjectStorage")
        {
            return new VideoDeliveryUrl(
                _objectStorage.GetPublicUrl(normalizedObjectName),
                ExpiresAt: null);
        }
        if (_settings.Mode != "SignedCdn")
        {
            throw new InvalidOperationException("Unsupported media delivery mode.");
        }

        var expiresAt = _timeProvider.GetUtcNow()
            .AddSeconds(_settings.TokenTtlSeconds);
        var expiry = expiresAt.ToUnixTimeSeconds();
        var keyId = _settings.KeyId!;
        var payload = $"v1\n{keyId}\n{expiry}\n/{normalizedPrefix}";
        using var hmac = new HMACSHA256(
            Encoding.UTF8.GetBytes(_settings.SigningSecret!));
        var signature = ToBase64Url(hmac.ComputeHash(Encoding.UTF8.GetBytes(payload)));
        var encodedObjectPath = string.Join(
            '/',
            normalizedObjectName.Split('/').Select(Uri.EscapeDataString));
        var url = $"{_settings.CdnBaseUrl!.TrimEnd('/')}/auth/" +
            $"{Uri.EscapeDataString(keyId)}/{signature}/{expiry}/{encodedObjectPath}";
        return new VideoDeliveryUrl(url, expiresAt);
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
    /// 将 HMAC 字节转换为无填充的 URL-safe Base64。
    /// </summary>
    private static string ToBase64Url(byte[] value)
        => Convert.ToBase64String(value)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
