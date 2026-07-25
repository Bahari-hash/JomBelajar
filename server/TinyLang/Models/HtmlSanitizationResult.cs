namespace TinyLang.Models;

/// <summary>
/// 描述 HTML 清理后的内容及从正文提取的安全检查信息。
/// </summary>
/// <param name="Html">清理后的 HTML。</param>
/// <param name="PlainText">从清理结果提取的纯文本。</param>
/// <param name="ImageSources">正文中保留的图片源地址。</param>
/// <param name="HasInvalidUrls">是否发现不允许的 URL。</param>
public sealed record HtmlSanitizationResult(
    string Html,
    string PlainText,
    IReadOnlyCollection<string> ImageSources,
    bool HasInvalidUrls);
