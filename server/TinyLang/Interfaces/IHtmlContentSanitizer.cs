using TinyLang.Models;

namespace TinyLang.Interfaces;

public interface IHtmlContentSanitizer
{
    HtmlSanitizationResult Sanitize(string html);
}
