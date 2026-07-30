using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;

namespace TinyLang.UnitTests;

/// <summary>
/// Verifies the canonical article Markdown syntax and server-side safety boundary.
/// </summary>
public sealed class ArticleMarkdownRendererTests
{
    private readonly ArticleMarkdownRenderer _renderer = new(new HtmlContentSanitizer());

    /// <summary>
    /// Verifies supported CommonMark constructs and managed image metadata are preserved deterministically.
    /// </summary>
    [Fact]
    public void RenderShouldSupportTheCanonicalMarkdownSubset()
    {
        const string imageUrl = "https://cdn.example.com/lesson.png";
        var markdown = """
            # Heading

            Paragraph with **bold**, *emphasis*, `code`, and [link](https://example.com/docs).

            > Quote

            - first
            - second

            1. ordered

            ```text
            Console.WriteLine("hello");
            ```

            ![Lesson](https://cdn.example.com/lesson.png "Title")
            """;

        var result = _renderer.Render(markdown);

        result.Html.Should().Contain("<h1>Heading</h1>");
        result.Html.Should().Contain("<strong>bold</strong>");
        result.Html.Should().Contain("<em>emphasis</em>");
        result.Html.Should().Contain("<blockquote>");
        result.Html.Should().Contain("<pre><code>");
        result.Html.Should().NotContain("class=");
        result.ImageSources.Should().ContainSingle().Which.Should().Be(imageUrl);
        result.PlainText.Should().Contain("Heading").And.Contain("Console.WriteLine");
    }

    /// <summary>
    /// Verifies raw HTML and executable attributes cannot enter canonical output.
    /// </summary>
    [Fact]
    public void RenderShouldDisableRawHtml()
    {
        var result = _renderer.Render("Safe text\n\n<script>alert(1)</script><b onclick=\"x()\">raw</b>");

        result.Html.Should().Contain("Safe text");
        result.Html.Should().NotContain("<script").And.NotContain("<b ");
        result.Html.Should().Contain("&lt;script&gt;");
    }

    /// <summary>
    /// Verifies relative and dangerous URLs fail instead of silently changing the saved article.
    /// </summary>
    [Theory]
    [InlineData("[relative](/docs)")]
    [InlineData("![relative](images/lesson.png)")]
    [InlineData("[danger](javascript:alert(1))")]
    public void RenderShouldRejectUnsupportedUrls(string markdown)
    {
        var action = () => _renderer.Render(markdown);

        action.Should().Throw<RequestValidationException>();
    }

    /// <summary>
    /// Verifies blank and oversized sources are rejected at the renderer boundary used by every workflow.
    /// </summary>
    [Fact]
    public void RenderShouldRejectBlankAndOversizedContent()
    {
        var blank = () => _renderer.Render("   ");
        var oversized = () => _renderer.Render(new string('a', ArticleConstraints.MaxContentLength + 1));

        blank.Should().Throw<RequestValidationException>();
        oversized.Should().Throw<RequestValidationException>();
    }
}
