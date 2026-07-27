using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义无 shell、带超时和取消控制的外部媒体进程执行边界。
/// </summary>
public interface IMediaProcessRunner
{
    /// <summary>
    /// 使用结构化参数启动进程并返回有界输出。
    /// </summary>
    /// <param name="executable">受配置控制的可执行程序路径。</param>
    /// <param name="arguments">逐项传递且不经过 shell 的参数。</param>
    /// <param name="timeout">进程最大运行时间。</param>
    /// <param name="cancellationToken">用于取消并终止进程树的令牌。</param>
    /// <returns>退出码和有界标准输出、标准错误。</returns>
    Task<MediaProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default);
}
