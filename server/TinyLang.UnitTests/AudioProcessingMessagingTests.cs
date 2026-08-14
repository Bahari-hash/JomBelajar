using FluentAssertions;
using MassTransit;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Services;
using TinyLang.Workers;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频任务 RabbitMQ 消息、调度和 consumer 幂等边界。
/// </summary>
public sealed class AudioProcessingMessagingTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 28, 12, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证 queue 只发布服务端生成标识和持久化 JobId。
    /// </summary>
    [Fact]
    public async Task QueueShouldPublishServerGeneratedJobMessage()
    {
        var jobId = Guid.NewGuid();
        AudioProcessingRequested? published = null;
        var endpoint = new Mock<IPublishEndpoint>();
        endpoint.Setup(value => value.Publish(
                It.IsAny<AudioProcessingRequested>(),
                It.IsAny<CancellationToken>()))
            .Callback<AudioProcessingRequested, CancellationToken>(
                (message, _) => published = message)
            .Returns(Task.CompletedTask);
        var queue = new MassTransitAudioProcessingQueue(
            endpoint.Object,
            new TestTimeProvider(Now));

        await queue.EnqueueAsync(jobId, TestContext.Current.CancellationToken);

        published.Should().NotBeNull();
        published!.Id.Should().NotBeEmpty();
        published.JobId.Should().Be(jobId);
        published.CreatedAt.Should().Be(Now);
    }

    /// <summary>
    /// 验证 dispatcher 只在成功发布后标记任务。
    /// </summary>
    [Fact]
    public async Task DispatcherShouldMarkSuccessfullyPublishedJob()
    {
        var jobId = Guid.NewGuid();
        var processingService = new Mock<IAudioProcessingService>();
        processingService.Setup(value => value.GetDispatchableJobIdsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([jobId]);
        var queue = new Mock<IAudioProcessingQueue>();
        queue.Setup(value => value.EnqueueAsync(
                jobId,
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dispatcher = new AudioProcessingDispatcher(
            processingService.Object,
            queue.Object);

        var count = await dispatcher.DispatchDueAsync(
            TestContext.Current.CancellationToken);

        count.Should().Be(1);
        processingService.Verify(value => value.MarkDispatchedAsync(
            jobId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    /// <summary>
    /// 验证无法领取的重复消息不会进入媒体处理。
    /// </summary>
    [Fact]
    public async Task ConsumerShouldIgnoreDuplicateMessage()
    {
        var message = new AudioProcessingRequested
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            CreatedAt = Now
        };
        var processingService = new Mock<IAudioProcessingService>();
        processingService.Setup(value => value.TryClaimAsync(
                message.JobId,
                message.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var context = new Mock<ConsumeContext<AudioProcessingRequested>>();
        context.SetupGet(value => value.Message).Returns(message);
        context.SetupGet(value => value.CancellationToken)
            .Returns(TestContext.Current.CancellationToken);
        var worker = new AudioProcessingWorker(
            processingService.Object,
            NullLogger<AudioProcessingWorker>.Instance);

        await worker.Consume(context.Object);

        processingService.Verify(value => value.ProcessClaimedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
