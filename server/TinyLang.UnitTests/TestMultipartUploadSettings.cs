using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 为上传相关测试提供稳定的 Multipart Upload 配置。
/// </summary>
internal static class TestMultipartUploadSettings
{
    /// <summary>
    /// 创建覆盖 1 GB 视频且具有较小测试批量的有效配置。
    /// </summary>
    public static MultipartUploadSettings Create() => new()
    {
        ThresholdMB = 64,
        PartSizeMB = 16,
        MaxPartCount = 10000,
        PartPresignBatchLimit = 20,
        SessionTtlMinutes = 1440,
        CleanupIntervalSeconds = 60,
        CleanupBatchSize = 50,
        FinalizationLeaseSeconds = 300,
        FinalizationMaxAttempts = 10,
        RetryMaxDelayMinutes = 60,
        MaxIncompleteUploadCountPerUser = 10,
        MaxIncompleteUploadMBPerUser = 2048
    };
}
