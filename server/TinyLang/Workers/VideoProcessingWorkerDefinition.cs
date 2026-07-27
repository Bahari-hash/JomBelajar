using MassTransit;
using Microsoft.Extensions.Options;
using TinyLang.Settings;

namespace TinyLang.Workers;

/// <summary>
/// 配置视频处理 consumer 的稳定 RabbitMQ endpoint 和有界并发。
/// </summary>
public sealed class VideoProcessingWorkerDefinition
    : ConsumerDefinition<VideoProcessingWorker>
{
    /// <summary>
    /// 使用视频处理配置创建 consumer definition。
    /// </summary>
    public VideoProcessingWorkerDefinition(
        IOptions<VideoProcessingSettings> options)
    {
        EndpointName = "tiny-lang-video-processing";
        ConcurrentMessageLimit = options.Value.MaxConcurrency;
    }
}
