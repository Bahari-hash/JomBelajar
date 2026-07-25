using TinyLang.Templates;

namespace TinyLang.Interfaces;

/// <summary>
/// 定义使用强类型模型渲染应用模板的契约。
/// </summary>
public interface ITemplateRenderer
{
    /// <summary>
    /// 加载并渲染指定模板。
    /// </summary>
    /// <typeparam name="TM">模板渲染模型类型。</typeparam>
    /// <param name="templateName">模板文件名。</param>
    /// <param name="model">传递给模板的模型。</param>
    /// <param name="cancellationToken">用于取消加载或渲染的令牌。</param>
    /// <returns>渲染后的文本。</returns>
    Task<string> RenderTemplateAsync<TM>(
        string templateName, TM model, CancellationToken cancellationToken = default)
        where TM : IEquatable<TM>, ITemplateRenderModel;
}
