namespace TinyLang.Interfaces;

/// <summary>
/// 定义按安全模板名称读取模板源内容的契约。
/// </summary>
public interface ITemplateContentProvider
{
    /// <summary>
    /// 从应用模板目录读取指定模板文本。
    /// </summary>
    /// <param name="templateName">不包含目录穿越信息的模板文件名。</param>
    /// <param name="cancellationToken">用于取消文件读取的令牌。</param>
    /// <returns>模板源文本。</returns>
    Task<string> GetContentAsync(string templateName, CancellationToken cancellationToken = default);
}
