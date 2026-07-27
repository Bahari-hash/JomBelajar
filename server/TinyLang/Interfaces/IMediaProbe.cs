using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义真实媒体探测、旋转尺寸折算和源视频规则校验契约。
/// </summary>
public interface IMediaProbe
{
    /// <summary>
    /// 探测并验证一个由服务端控制路径指向的本地媒体文件。
    /// </summary>
    /// <param name="inputPath">worker 下载的本地源文件路径。</param>
    /// <param name="cancellationToken">用于取消探测的令牌。</param>
    /// <returns>经过验证的媒体属性。</returns>
    Task<MediaProbeResult> ProbeAsync(
        string inputPath,
        CancellationToken cancellationToken = default);
}
