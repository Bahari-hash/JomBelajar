using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义生成标准化单声道 MP3 的音频转码契约。
/// </summary>
public interface IAudioTranscoder
{
    /// <summary>
    /// 将已验证源文件转码为固定参数的本地 MP3 输出。
    /// </summary>
    Task<AudioTranscodeResult> TranscodeAsync(
        string inputPath,
        string outputDirectory,
        CancellationToken cancellationToken = default);
}
