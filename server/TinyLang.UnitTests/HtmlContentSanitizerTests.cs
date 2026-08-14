using FluentAssertions;
using TinyLang.Infrastructure;

namespace TinyLang.UnitTests;

public sealed class HtmlContentSanitizerTests
{
    private readonly HtmlContentSanitizer _sanitizer = new();

    [Fact]
    public void ShouldRemoveScriptsEventHandlersStylesAndEmbeddedContent()
    {
        const string html = """
            <p onclick="alert(1)" style="color:red" title="not-needed">Safe</p>
            <script>alert(1)</script>
            <iframe src="https://evil.example"></iframe>
            <form><input name="secret"></form>
            """;

        var result = _sanitizer.Sanitize(html);

        result.Html.Should().Contain("Safe");
        result.Html.Contains("onclick", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("style=", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("title=", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("script", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("iframe", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("form", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("input", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
    }

    [Fact]
    public void ShouldRejectDangerousAndRelativeUrls()
    {
        const string html = """
            <a href="javascript:alert(1)">Bad link</a>
            <img src="data:image/png;base64,AAAA">
            <img src="/relative/image.png">
            """;

        var result = _sanitizer.Sanitize(html);

        result.HasInvalidUrls.Should().BeTrue();
        result.ImageSources.Should().BeEmpty();
        result.Html.Contains("javascript:", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("data:", StringComparison.OrdinalIgnoreCase).Should().BeFalse();
        result.Html.Contains("/relative/image.png", StringComparison.Ordinal).Should().BeFalse();
    }

    [Fact]
    public void ShouldPreserveAllowedMarkupAndReturnDistinctImageSources()
    {
        const string imageUrl = "https://oss.example.com/articles/image.png";
        const string html = """
            <h2>Heading</h2>
            <p><strong>Text</strong> <a href="https://example.com">Link</a></p>
            <img src="https://oss.example.com/articles/image.png" alt="Image">
            <img src="https://oss.example.com/articles/image.png" alt="Repeated">
            """;

        var result = _sanitizer.Sanitize(html);

        result.HasInvalidUrls.Should().BeFalse();
        result.Html.Should().Contain("<h2>Heading</h2>");
        result.Html.Should().Contain("<strong>Text</strong>");
        result.ImageSources.Should().Equal(imageUrl);
    }
}
