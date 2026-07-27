namespace TinyLang.Models;

/// <summary>
/// 描述 ffprobe 验证后的源视频媒体属性。
/// </summary>
/// <param name="DurationSeconds">有效播放时长。</param>
/// <param name="DisplayWidth">计入旋转后的显示宽度。</param>
/// <param name="DisplayHeight">计入旋转后的显示高度。</param>
/// <param name="ContainerFormat">容器格式摘要。</param>
/// <param name="VideoCodec">主视频流 codec。</param>
/// <param name="AudioCodec">默认音频流 codec。</param>
public sealed record MediaProbeResult(
    double DurationSeconds,
    int DisplayWidth,
    int DisplayHeight,
    string ContainerFormat,
    string VideoCodec,
    string AudioCodec);

/// <summary>
/// 描述一条不会放大源视频的目标 HLS rendition。
/// </summary>
/// <param name="Label">用于服务端输出目录的稳定标签。</param>
/// <param name="TargetHeight">用于业务展示和唯一约束的目标高度。</param>
/// <param name="Width">实际偶数输出宽度。</param>
/// <param name="Height">实际偶数输出高度。</param>
/// <param name="VideoBitrateKbps">视频编码码率。</param>
/// <param name="AudioBitrateKbps">AAC 音频码率。</param>
/// <param name="Codecs">master playlist 中声明的 codec 字符串。</param>
public sealed record VideoRenditionPlan(
    string Label,
    int TargetHeight,
    int Width,
    int Height,
    int VideoBitrateKbps,
    int AudioBitrateKbps,
    string Codecs);

/// <summary>
/// 描述 FFmpeg 生成的一条本地 HLS rendition。
/// </summary>
/// <param name="Plan">实际使用的编码计划。</param>
/// <param name="PlaylistPath">本地 variant playlist 路径。</param>
public sealed record GeneratedVideoRendition(
    VideoRenditionPlan Plan,
    string PlaylistPath);

/// <summary>
/// 描述一次完整本地 HLS 处理的关键文件和 rendition 集合。
/// </summary>
/// <param name="MasterPlaylistPath">最后发布的本地 master playlist。</param>
/// <param name="PosterPath">本地 JPEG poster。</param>
/// <param name="Renditions">实际生成的 rendition。</param>
public sealed record VideoTranscodeResult(
    string MasterPlaylistPath,
    string PosterPath,
    IReadOnlyList<GeneratedVideoRendition> Renditions);

/// <summary>
/// 表示受控外部媒体进程的退出码和有界输出。
/// </summary>
/// <param name="ExitCode">进程退出码。</param>
/// <param name="StandardOutput">有界标准输出。</param>
/// <param name="StandardError">有界标准错误。</param>
public sealed record MediaProcessResult(
    int ExitCode,
    string StandardOutput,
    string StandardError);
