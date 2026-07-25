using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TinyLang.Enums;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

public sealed class VerificationCodeSenderTests
{
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

    private sealed class AtomicStore(string? value) : IVerificationCodeStore
    {
        private readonly object _sync = new();
        private string? _value = value;

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
    }

    private sealed class MockTemplateRenderer : ITemplateRenderer
    {
        public static readonly MockTemplateRenderer Instance = new();

        public Task<string> RenderTemplateAsync<TM>(string templateName, TM model,
            CancellationToken cancellationToken = default)
            where TM : IEquatable<TM>, TinyLang.Templates.ITemplateRenderModel
            => throw new NotSupportedException();
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
