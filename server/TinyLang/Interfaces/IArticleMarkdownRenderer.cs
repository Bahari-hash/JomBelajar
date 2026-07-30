using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// Defines the canonical server-side Markdown rendering and content-safety boundary for articles.
/// </summary>
public interface IArticleMarkdownRenderer
{
    /// <summary>
    /// Renders Markdown with the supported syntax subset and returns sanitized HTML and extracted metadata.
    /// </summary>
    /// <param name="markdown">The untrusted Markdown source.</param>
    /// <returns>The canonical sanitized render result.</returns>
    ArticleContentRenderResult Render(string markdown);
}
