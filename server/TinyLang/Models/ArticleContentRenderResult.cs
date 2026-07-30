namespace TinyLang.Models;

/// <summary>
/// Describes canonical article HTML and the metadata extracted from its sanitized representation.
/// </summary>
/// <param name="Html">The canonical sanitized HTML.</param>
/// <param name="PlainText">The plain text extracted from the sanitized HTML.</param>
/// <param name="ImageSources">The distinct absolute image URLs referenced by the article body.</param>
public sealed record ArticleContentRenderResult(
    string Html,
    string PlainText,
    IReadOnlyCollection<string> ImageSources);
