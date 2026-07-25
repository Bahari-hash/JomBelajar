namespace TinyLang.Templates;

/// <summary>
/// 定义可由模板渲染器处理的邮件模型元数据。
/// </summary>
public interface ITemplateRenderModel
{
    string TemplateName { get; }
    string Subject { get; }
}
