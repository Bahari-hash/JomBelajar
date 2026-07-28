namespace TinyLang.Interfaces;

/// <summary>
/// 定义音频 worker 启动前的 ffprobe、FFmpeg 和 MP3 encoder 检查。
/// </summary>
public interface IAudioToolPreflight
{
    /// <summary>
    /// 验证配置工具可启动且 FFmpeg 提供 libmp3lame encoder。
    /// </summary>
    Task ValidateAsync(CancellationToken cancellationToken = default);
}
