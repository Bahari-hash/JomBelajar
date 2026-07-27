namespace TinyLang.Constants;

/// <summary>
/// 定义可附加到 endpoint 的限流策略名称。
/// </summary>
public static class RateLimitPolicies
{
    public const string StrictCodeLimit = "StrictCodeLimit";
    public const string UploadPresignLimit = "UploadPresignLimit";
    public const string UploadCommandLimit = "UploadCommandLimit";
    public const string VideoPlaybackLimit = "VideoPlaybackLimit";
    public const string VideoProgressLimit = "VideoProgressLimit";
}
