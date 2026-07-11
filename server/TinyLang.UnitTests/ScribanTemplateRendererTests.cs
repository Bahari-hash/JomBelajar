using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Templates;

namespace TinyLang.UnitTests;

public sealed class ScribanTemplateRendererTests
{
    private sealed class FakeContentProvider(string content) : ITemplateContentProvider
    {
        public Task<string> GetContentAsync(
            string templateName, CancellationToken ct = default)
        {
            return Task.FromResult(content);
        }
    }

    [Fact]
    public async Task ShouldRenderAuthCodeModel()
    {
        var model = new VerificationCodeRenderModel
        {
            UserEmail = "noreply@tinylang.com",
            Code = "556677",
            ExpiryMinutes = 15
        };

        var templateContent = @"
            <div>
                <h2>Hello {{ user_email }},</h2>
                <p>Your secure code is: <b>{{ code }}</b></p>
                <p>It will expire in {{ expiry_minutes }} minutes.</p>
            </div>";
        var contentProvider = new FakeContentProvider(templateContent);
        var fakeLogger = NullLogger<ScribanTemplateRenderer>.Instance;

        var render = new ScribanTemplateRenderer(contentProvider, fakeLogger);
        var ct = TestContext.Current.CancellationToken;
        var result = await render.RenderTemplateAsync(model.TemplateName, model, ct);

        result.Should().NotBeNullOrEmpty();
        result.Should().Contain($"<h2>Hello {model.UserEmail},</h2>");
        result.Should().Contain($"<p>Your secure code is: <b>{model.Code}</b></p>");
        result.Should().Contain($"<p>It will expire in {model.ExpiryMinutes} minutes.</p>");
    }
}
