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
/// 验证视频任务 RabbitMQ 调度和 consumer 的幂等处理边界。
/// </summary>
public sealed class VideoProcessingMessagingTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 27, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task QueueShouldPublishServerGeneratedJobMessage()
    {
        var jobId = Guid.NewGuid();
        VideoProcessingRequested? published = null;
        var endpoint = new Mock<IPublishEndpoint>();
        endpoint.Setup(value => value.Publish(
                It.IsAny<VideoProcessingRequested>(),
                It.IsAny<CancellationToken>()))
            .Callback<VideoProcessingRequested, CancellationToken>(
                (message, _) => published = message)
            .Returns(Task.CompletedTask);
        var queue = new MassTransitVideoProcessingQueue(
            endpoint.Object,
            new TestTimeProvider(Now));

        await queue.EnqueueAsync(jobId, TestContext.Current.CancellationToken);

        published.Should().NotBeNull();
        published!.Id.Should().NotBeEmpty();
        published.JobId.Should().Be(jobId);
        published.CreatedAt.Should().Be(Now);
    }

    [Fact]
    public async Task DispatcherShouldMarkEverySuccessfullyPublishedJob()
    {
        var firstJobId = Guid.NewGuid();
        var secondJobId = Guid.NewGuid();
        var processingService = new Mock<IVideoProcessingService>();
        processingService.Setup(value => value.GetDispatchableJobIdsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([firstJobId, secondJobId]);
        var queue = new Mock<IVideoProcessingQueue>();
        queue.Setup(value => value.EnqueueAsync(
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        var dispatcher = new VideoProcessingDispatcher(
            processingService.Object,
            queue.Object);

        var result = await dispatcher.DispatchDueAsync(
            TestContext.Current.CancellationToken);

        result.Should().Be(2);
        queue.Verify(value => value.EnqueueAsync(
            firstJobId,
            It.IsAny<CancellationToken>()), Times.Once);
        queue.Verify(value => value.EnqueueAsync(
            secondJobId,
            It.IsAny<CancellationToken>()), Times.Once);
        processingService.Verify(value => value.MarkDispatchedAsync(
            firstJobId,
            It.IsAny<CancellationToken>()), Times.Once);
        processingService.Verify(value => value.MarkDispatchedAsync(
            secondJobId,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DispatcherShouldNotMarkJobWhenPublishFails()
    {
        var jobId = Guid.NewGuid();
        var processingService = new Mock<IVideoProcessingService>();
        processingService.Setup(value => value.GetDispatchableJobIdsAsync(
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([jobId]);
        var queue = new Mock<IVideoProcessingQueue>();
        queue.Setup(value => value.EnqueueAsync(
                jobId,
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("broker unavailable"));
        var dispatcher = new VideoProcessingDispatcher(
            processingService.Object,
            queue.Object);

        var action = () => dispatcher.DispatchDueAsync(
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<InvalidOperationException>();
        processingService.Verify(value => value.MarkDispatchedAsync(
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConsumerShouldProcessSuccessfullyClaimedJob()
    {
        var message = CreateMessage();
        var processingService = new Mock<IVideoProcessingService>();
        processingService.Setup(value => value.TryClaimAsync(
                message.JobId,
                message.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var worker = new VideoProcessingWorker(
            processingService.Object,
            NullLogger<VideoProcessingWorker>.Instance);

        await worker.Consume(CreateContext(message).Object);

        processingService.Verify(value => value.ProcessClaimedAsync(
            message.JobId,
            message.Id,
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ConsumerShouldIgnoreDuplicateMessageThatCannotClaimJob()
    {
        var message = CreateMessage();
        var processingService = new Mock<IVideoProcessingService>();
        processingService.Setup(value => value.TryClaimAsync(
                message.JobId,
                message.Id,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var worker = new VideoProcessingWorker(
            processingService.Object,
            NullLogger<VideoProcessingWorker>.Instance);

        await worker.Consume(CreateContext(message).Object);

        processingService.Verify(value => value.ProcessClaimedAsync(
            It.IsAny<Guid>(),
            It.IsAny<Guid>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    /// <summary>
    /// 创建不包含业务或对象存储信息的最小视频任务消息。
    /// </summary>
    private static VideoProcessingRequested CreateMessage()
        => new()
        {
            Id = Guid.NewGuid(),
            JobId = Guid.NewGuid(),
            CreatedAt = DateTimeOffset.UtcNow
        };

    /// <summary>
    /// 创建提供固定消息和测试取消令牌的 MassTransit 消费上下文。
    /// </summary>
    private static Mock<ConsumeContext<VideoProcessingRequested>> CreateContext(
        VideoProcessingRequested message)
    {
        var context = new Mock<ConsumeContext<VideoProcessingRequested>>();
        context.SetupGet(value => value.Message).Returns(message);
        context.SetupGet(value => value.CancellationToken)
            .Returns(TestContext.Current.CancellationToken);
        return context;
    }
}
