using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using TinyLang.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class VerificationCodeSenderTests
{
    [Fact]
    public async Task DifferentEmailsShouldHaveIndependentSendCooldowns()
    {
        var store = new Mock<IVerificationCodeStore>();
        store.Setup(x => x.TryAcquireSendCooldownAsync(
                It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        var emailSender = new Mock<IEmailSender>();
        var sender = new VerificationCodeSender(
            new VerificationCodeGenerator(),
            store.Object,
            Options.Create(new VerificationCodeSettings { CodeLength = 6, ExpMinutes = 10 }),
            new SecretHasher(),
            MockTemplateRenderer.Instance,
            emailSender.Object);

        await sender.SendCodeAsync(
            "first@example.com", VerificationCodePurpose.Register, TestContext.Current.CancellationToken);
        await sender.SendCodeAsync(
            "second@example.com", VerificationCodePurpose.ResetPassword, TestContext.Current.CancellationToken);

        store.Verify(x => x.TryAcquireSendCooldownAsync(
            "first@example.com", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        store.Verify(x => x.TryAcquireSendCooldownAsync(
            "second@example.com", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()), Times.Once);
        emailSender.Verify(x => x.EnqueueEmailAsync(
            It.IsAny<TinyLang.Models.EmailMessage>(), It.IsAny<CancellationToken>()), Times.Exactly(2));
    }

    [Fact]
    public async Task RepeatedEmailShouldBeRejectedBeforeGeneratingAndSendingAnotherCode()
    {
        var store = new Mock<IVerificationCodeStore>();
        store.Setup(x => x.TryAcquireSendCooldownAsync(
                "user@example.com", It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        var emailSender = new Mock<IEmailSender>();
        var sender = new VerificationCodeSender(
            new VerificationCodeGenerator(),
            store.Object,
            Options.Create(new VerificationCodeSettings { CodeLength = 6, ExpMinutes = 10 }),
            new SecretHasher(),
            MockTemplateRenderer.Instance,
            emailSender.Object);

        var action = () => sender.SendCodeAsync(
            "user@example.com", VerificationCodePurpose.Register, TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<TooManyRequestsException>();
        emailSender.Verify(x => x.EnqueueEmailAsync(
            It.IsAny<TinyLang.Models.EmailMessage>(), It.IsAny<CancellationToken>()), Times.Never);
        store.Verify(x => x.SaveAsync(
            It.IsAny<string>(), It.IsAny<VerificationCodePurpose>(), It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ConcurrentCorrectVerificationsShouldConsumeCodeOnlyOnce()
    {
        var code = "123456";
        var hasher = new SecretHasher();
        var store = new AtomicStore(hasher.Hash(code));
        var sender = new VerificationCodeSender(
            new VerificationCodeGenerator(),
            store,
            Options.Create(new VerificationCodeSettings { CodeLength = 6, ExpMinutes = 10 }),
            hasher,
            MockTemplateRenderer.Instance,
            MockEmailSender.Instance);

        var cancellationToken = TestContext.Current.CancellationToken;
        var results = await Task.WhenAll(
            sender.VerifyCodeAsync(
                "user@example.com", VerificationCodePurpose.Register, code, cancellationToken),
            sender.VerifyCodeAsync(
                "user@example.com", VerificationCodePurpose.Register, code, cancellationToken));

        results.Count(x => x).Should().Be(1);
    }

    [Fact]
    public async Task ReachingFailureThresholdShouldInvalidateVerificationCode()
    {
        var hasher = new SecretHasher();
        var code = "123456";
        var store = new Mock<IVerificationCodeStore>();
        store.Setup(x => x.GetAsync(
                "user@example.com",
                VerificationCodePurpose.Register,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(hasher.Hash(code));
        store.Setup(x => x.TryConsumeAsync(
                "user@example.com",
                VerificationCodePurpose.Register,
                It.IsAny<string>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(false);
        store.Setup(x => x.IncrementFailureAsync(
                "user@example.com",
                VerificationCodePurpose.Register,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(5);
        var sender = new VerificationCodeSender(
            new VerificationCodeGenerator(),
            store.Object,
            Options.Create(new VerificationCodeSettings
            {
                CodeLength = 6,
                ExpMinutes = 10,
                MaxFailedAttempts = 5
            }),
            hasher,
            MockTemplateRenderer.Instance,
            MockEmailSender.Instance);

        var result = await sender.VerifyCodeAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            "000000",
            TestContext.Current.CancellationToken);

        result.Should().BeFalse();
        store.Verify(x => x.DeleteAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            TestContext.Current.CancellationToken), Times.Once);
    }

    [Fact]
    public async Task SuccessfulVerificationShouldResetFailureCounter()
    {
        var hasher = new SecretHasher();
        var code = "123456";
        var store = new Mock<IVerificationCodeStore>();
        store.Setup(x => x.GetAsync(
                "user@example.com",
                VerificationCodePurpose.Register,
                TestContext.Current.CancellationToken))
            .ReturnsAsync(hasher.Hash(code));
        store.Setup(x => x.TryConsumeAsync(
                "user@example.com",
                VerificationCodePurpose.Register,
                It.IsAny<string>(),
                TestContext.Current.CancellationToken))
            .ReturnsAsync(true);
        var sender = new VerificationCodeSender(
            new VerificationCodeGenerator(),
            store.Object,
            Options.Create(new VerificationCodeSettings
            {
                CodeLength = 6,
                ExpMinutes = 10
            }),
            hasher,
            MockTemplateRenderer.Instance,
            MockEmailSender.Instance);

        var result = await sender.VerifyCodeAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            code,
            TestContext.Current.CancellationToken);

        result.Should().BeTrue();
        store.Verify(x => x.ResetFailuresAsync(
            "user@example.com",
            VerificationCodePurpose.Register,
            TestContext.Current.CancellationToken), Times.Once);
        store.Verify(x => x.IncrementFailureAsync(
            It.IsAny<string>(),
            It.IsAny<VerificationCodePurpose>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    private sealed class AtomicStore(string? value) : IVerificationCodeStore
    {
        private readonly object _sync = new();
        private string? _value = value;

        public Task<bool> TryAcquireSendCooldownAsync(string email, TimeSpan window,
            CancellationToken cancellationToken = default)
            => Task.FromResult(true);

        public Task SaveAsync(string email, VerificationCodePurpose purpose, string code,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<string?> GetAsync(string email, VerificationCodePurpose purpose,
            CancellationToken cancellationToken = default)
            => Task.FromResult(_value);

        public Task<bool> TryConsumeAsync(string email, VerificationCodePurpose purpose,
            string expectedValue, CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                if (_value != expectedValue)
                {
                    return Task.FromResult(false);
                }

                _value = null;
                return Task.FromResult(true);
            }
        }

        public Task<int> IncrementFailureAsync(
            string email,
            VerificationCodePurpose purpose,
            CancellationToken cancellationToken = default)
            => Task.FromResult(1);

        public Task ResetFailuresAsync(
            string email,
            VerificationCodePurpose purpose,
            CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task DeleteAsync(
            string email,
            VerificationCodePurpose purpose,
            CancellationToken cancellationToken = default)
        {
            lock (_sync)
            {
                _value = null;
            }
            return Task.CompletedTask;
        }
    }

    private sealed class MockTemplateRenderer : ITemplateRenderer
    {
        public static readonly MockTemplateRenderer Instance = new();

        public Task<string> RenderTemplateAsync<TM>(string templateName, TM model,
            CancellationToken cancellationToken = default)
            where TM : IEquatable<TM>, TinyLang.Templates.ITemplateRenderModel
            => Task.FromResult("verification email");
    }

    private sealed class MockEmailSender : IEmailSender
    {
        public static readonly MockEmailSender Instance = new();

        public Task SendEmailAsync(TinyLang.Models.EmailMessage message,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task EnqueueEmailAsync(TinyLang.Models.EmailMessage message,
            CancellationToken cancellationToken = default)
            => throw new NotSupportedException();
    }
}
