using Markdig;
using TinyLang.Dtos;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

/// <summary>
/// Uses a restricted Markdig pipeline and the shared HTML sanitizer to produce canonical article content.
/// </summary>
/// <param name="htmlSanitizer">The allowlist sanitizer applied after Markdown rendering.</param>
public sealed class ArticleMarkdownRenderer(IHtmlContentSanitizer htmlSanitizer)
    : IArticleMarkdownRenderer
{
    private static readonly MarkdownPipeline Pipeline = new MarkdownPipelineBuilder()
        .DisableHtml()
        .Build();

    /// <inheritdoc />
    public ArticleContentRenderResult Render(string markdown)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            throw new RequestValidationException(ErrorCodes.ArticleContentRequired);
        }
        if (markdown.Length > ArticleConstraints.MaxContentLength)
        {
            throw new RequestValidationException(ErrorCodes.ArticleContentLengthLimit);
        }

        var renderedHtml = Markdown.ToHtml(markdown, Pipeline);
        var sanitized = htmlSanitizer.Sanitize(renderedHtml);
        if (sanitized.Html.Length > ArticleConstraints.MaxContentLength)
        {
            throw new RequestValidationException(ErrorCodes.ArticleContentLengthLimit);
        }
        if (sanitized.HasInvalidUrls ||
            (string.IsNullOrWhiteSpace(sanitized.PlainText) && sanitized.ImageSources.Count == 0))
        {
            throw new RequestValidationException(ErrorCodes.ArticleContentInvalid);
        }

        return new ArticleContentRenderResult(
            sanitized.Html,
            sanitized.PlainText,
            sanitized.ImageSources);
    }
}
