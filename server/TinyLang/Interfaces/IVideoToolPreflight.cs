namespace TinyLang.Interfaces;

/// <summary>
/// 定义视频 worker 启动时验证 ffprobe 和 FFmpeg 可执行性的边界。
/// </summary>
public interface IVideoToolPreflight
{
    /// <summary>
    /// 验证配置的媒体工具可启动且返回成功版本信息。
    /// </summary>
    /// <param name="cancellationToken">用于取消启动检查的令牌。</param>
    /// <returns>表示两个工具均可用的任务。</returns>
    Task ValidateAsync(CancellationToken cancellationToken = default);
}
