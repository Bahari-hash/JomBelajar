using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义源音频真实流探测和业务限制校验契约。
/// </summary>
public interface IAudioProbe
{
    /// <summary>
    /// 探测并验证一个由 worker 控制路径指向的本地音频文件。
    /// </summary>
    Task<AudioProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}
