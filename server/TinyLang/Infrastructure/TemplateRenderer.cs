using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Scriban;
using TinyLang.Interfaces;
using TinyLang.Templates;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 Scriban 解析、缓存并渲染强类型模板。
/// </summary>
/// <param name="templateProvider">模板源内容 provider。</param>
/// <param name="logger">模板解析日志记录器。</param>
public sealed class TemplateRenderer(
    ITemplateContentProvider templateProvider, ILogger<TemplateRenderer> logger) : ITemplateRenderer
{
    private readonly ITemplateContentProvider _templateProvider = templateProvider;
    private readonly ConcurrentDictionary<string, Lazy<Task<Template>>> _templateCache = new();

    /// <inheritdoc />
    public async Task<string> RenderTemplateAsync<TM>(
        string templateName, TM model, CancellationToken cancellationToken = default)
        where TM : IEquatable<TM>, ITemplateRenderModel
    {
        var lazyTemplate = _templateCache.GetOrAdd(templateName, name =>
            new Lazy<Task<Template>>(() => ParseTemplateAsync(name, cancellationToken)));

        var template = await lazyTemplate.Value;
        return await template.RenderAsync(model);
    }

    /// <summary>
    /// 加载并解析 Scriban 模板，在解析失败时记录诊断信息。
    /// </summary>
    /// <param name="templateName">模板文件名。</param>
    /// <param name="cancellationToken">用于取消模板读取的令牌。</param>
    /// <returns>已成功解析的模板。</returns>
    /// <exception cref="InvalidOperationException">模板包含 Scriban 解析错误。</exception>
    private async Task<Template> ParseTemplateAsync(string templateName, CancellationToken cancellationToken)
    {
        var content = await _templateProvider.GetContentAsync(templateName, cancellationToken);
        var template = Template.Parse(content, templateName);

        if (template.HasErrors)
        {
            var errors = string.Join("; ", template.Messages);

            logger.LogError("Failed to parse template file '{TemplateName}: {Errors}'", templateName, errors);
            throw new InvalidOperationException($"Cannot parse template file '{templateName}': {errors}");
        }

        return template;
    }
}
