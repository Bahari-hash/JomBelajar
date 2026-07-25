using TinyLang.Models;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义文章 HTML 清理和正文元数据提取契约。
/// </summary>
public interface IHtmlContentSanitizer
{
    /// <summary>
    /// 清理不受支持的 HTML，并提取纯文本、图片源和无效 URL 状态。
    /// </summary>
    /// <param name="html">待清理的 HTML。</param>
    /// <returns>清理后的内容及检查结果。</returns>
    HtmlSanitizationResult Sanitize(string html);
}
