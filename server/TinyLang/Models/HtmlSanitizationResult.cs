namespace TinyLang.Models;

public sealed record HtmlSanitizationResult(
    string Html,
    string PlainText,
    IReadOnlyCollection<string> ImageSources,
    bool HasInvalidUrls);
