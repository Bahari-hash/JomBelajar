using MassTransit;
using Microsoft.Extensions.Options;
using TinyLang.Settings;

namespace TinyLang.Workers;

/// <summary>
/// 配置音频处理 consumer 的稳定 RabbitMQ endpoint 和有界并发。
/// </summary>
public sealed class AudioProcessingWorkerDefinition
    : ConsumerDefinition<AudioProcessingWorker>
{
    /// <summary>
    /// 使用音频处理配置创建 consumer definition。
    /// </summary>
    public AudioProcessingWorkerDefinition(
        IOptions<AudioProcessingSettings> options)
    {
        EndpointName = "tiny-lang-audio-processing";
        ConcurrentMessageLimit = options.Value.MaxConcurrency;
    }
}
