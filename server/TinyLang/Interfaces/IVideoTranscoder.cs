using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义从已探测本地源文件生成版本化 HLS 和 poster 的处理契约。
/// </summary>
public interface IVideoTranscoder
{
    /// <summary>
    /// 在隔离输出目录生成不放大的 HLS renditions、master 和 poster。
    /// </summary>
    /// <param name="inputPath">本地源文件路径。</param>
    /// <param name="outputDirectory">当前 job 独占的输出目录。</param>
    /// <param name="probeResult">已经验证的源媒体属性。</param>
    /// <param name="cancellationToken">用于取消转码并终止进程树的令牌。</param>
    /// <returns>生成的本地关键文件和 rendition 信息。</returns>
    Task<VideoTranscodeResult> TranscodeAsync(
        string inputPath,
        string outputDirectory,
        MediaProbeResult probeResult,
        CancellationToken cancellationToken = default);
}
