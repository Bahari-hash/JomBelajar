using System.Diagnostics;
using System.IO;
using System.Text;
using Microsoft.Extensions.Options;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 Process API 无 shell 地运行 ffprobe/FFmpeg，并限制诊断输出大小。
/// </summary>
public sealed class MediaProcessRunner : IMediaProcessRunner
{
    private readonly int _maxOutputCharacters;

    /// <summary>
    /// 使用视频处理配置创建进程执行器。
    /// </summary>
    /// <param name="options">诊断输出大小等视频处理配置。</param>
    public MediaProcessRunner(IOptions<VideoProcessingSettings> options)
    {
        _maxOutputCharacters = options.Value.MaxDiagnosticOutputCharacters;
    }

    /// <inheritdoc />
    public async Task<MediaProcessResult> RunAsync(
        string executable,
        IReadOnlyList<string> arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken = default)
    {
        using var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executable,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            }
        };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }

        try
        {
            if (!process.Start())
            {
                throw new VideoProcessingException(
                    VideoProcessingFailureCode.ProcessStartFailed,
                    isTransient: true);
            }
        }
        catch (VideoProcessingException)
        {
            throw;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new VideoProcessingException(
                VideoProcessingFailureCode.ProcessStartFailed,
                isTransient: true);
        }

        using var timeoutSource = new CancellationTokenSource(timeout);
        using var linkedSource = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutSource.Token);
        var stdoutTask = ReadBoundedAsync(
            process.StandardOutput,
            _maxOutputCharacters,
            CancellationToken.None);
        var stderrTask = ReadBoundedAsync(
            process.StandardError,
            _maxOutputCharacters,
            CancellationToken.None);
        try
        {
            await process.WaitForExitAsync(linkedSource.Token);
        }
        catch (OperationCanceledException) when (timeoutSource.IsCancellationRequested &&
            !cancellationToken.IsCancellationRequested)
        {
            KillProcessTree(process);
            await Task.WhenAll(stdoutTask, stderrTask);
            throw new VideoProcessingException(
                VideoProcessingFailureCode.ProcessTimedOut,
                isTransient: true);
        }
        catch (OperationCanceledException)
        {
            KillProcessTree(process);
            await Task.WhenAll(stdoutTask, stderrTask);
            throw;
        }

        return new MediaProcessResult(
            process.ExitCode,
            await stdoutTask,
            await stderrTask);
    }

    /// <summary>
    /// 持续排空进程输出，同时只保留配置上限内的字符。
    /// </summary>
    private static async Task<string> ReadBoundedAsync(
        StreamReader reader,
        int maxCharacters,
        CancellationToken cancellationToken)
    {
        var retained = new StringBuilder(Math.Min(maxCharacters, 4096));
        var buffer = new char[4096];
        int read;
        while ((read = await reader.ReadAsync(buffer, cancellationToken)) > 0)
        {
            var remaining = maxCharacters - retained.Length;
            if (remaining > 0)
            {
                retained.Append(buffer, 0, Math.Min(read, remaining));
            }
        }
        return retained.ToString();
    }

    /// <summary>
    /// 尽力终止当前媒体进程及其所有子进程。
    /// </summary>
    private static void KillProcessTree(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch (InvalidOperationException)
        {
        }
    }
}
