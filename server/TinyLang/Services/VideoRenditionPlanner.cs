using Microsoft.Extensions.Options;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 根据旋转后的源显示尺寸选择不放大的 480p、720p 和 1080p 输出计划。
/// </summary>
public sealed class VideoRenditionPlanner
{
    private readonly VideoProcessingSettings _settings;

    /// <summary>
    /// 使用配置的三档码率和 AAC 码率创建规划器。
    /// </summary>
    public VideoRenditionPlanner(IOptions<VideoProcessingSettings> options)
    {
        _settings = options.Value;
    }

    /// <summary>
    /// 生成至少一条、保持宽高比且所有尺寸均为偶数的 rendition 计划。
    /// </summary>
    /// <param name="displayWidth">源视频实际显示宽度。</param>
    /// <param name="displayHeight">源视频实际显示高度。</param>
    /// <returns>按高度从低到高排列的输出计划。</returns>
    public IReadOnlyList<VideoRenditionPlan> CreatePlan(
        int displayWidth,
        int displayHeight)
    {
        if (displayWidth <= 0 || displayHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(displayWidth),
                "Display dimensions must be positive.");
        }

        var targets = new[]
        {
            (Height: 480, Bitrate: _settings.VideoBitrate480Kbps),
            (Height: 720, Bitrate: _settings.VideoBitrate720Kbps),
            (Height: 1080, Bitrate: _settings.VideoBitrate1080Kbps)
        };
        var plans = targets
            .Where(target => displayHeight >= target.Height)
            .Select(target => CreatePlan(
                displayWidth,
                displayHeight,
                target.Height,
                target.Bitrate,
                $"{target.Height}p"))
            .ToList();
        if (plans.Count == 0)
        {
            var height = MakeEven(displayHeight);
            plans.Add(CreatePlan(
                displayWidth,
                displayHeight,
                height,
                _settings.VideoBitrate480Kbps,
                $"source-{height}p"));
        }
        return plans;
    }

    /// <summary>
    /// 按源宽高比计算单条偶数尺寸和 codec 声明。
    /// </summary>
    private VideoRenditionPlan CreatePlan(
        int sourceWidth,
        int sourceHeight,
        int targetHeight,
        int bitrate,
        string label)
    {
        var height = MakeEven(Math.Min(sourceHeight, targetHeight));
        var scaledWidth = (double)sourceWidth * height / sourceHeight;
        var nearestEvenWidth = 2 * (int)Math.Round(
            scaledWidth / 2,
            MidpointRounding.AwayFromZero);
        var width = Math.Max(
            2,
            Math.Min(MakeEven(sourceWidth), nearestEvenWidth));
        var videoCodec = height >= 1080 ? "avc1.640028" : "avc1.64001f";
        return new VideoRenditionPlan(
            label,
            targetHeight,
            width,
            height,
            bitrate,
            _settings.AudioBitrateKbps,
            $"{videoCodec},mp4a.40.2");
    }

    /// <summary>
    /// 向下舍入为 FFmpeg 编码器普遍支持的正偶数。
    /// </summary>
    private static int MakeEven(int value) => Math.Max(2, value - value % 2);
}
