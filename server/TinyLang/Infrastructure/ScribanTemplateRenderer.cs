using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Scriban;
using TinyLang.Interfaces;
using TinyLang.Templates;

namespace TinyLang.Infrastructure;

public sealed class ScribanTemplateRenderer(
    ITemplateContentProvider templateProvider, ILogger<ScribanTemplateRenderer> logger) : ITemplateRenderer
{
    private readonly ITemplateContentProvider _templateProvider = templateProvider;
    private readonly ConcurrentDictionary<string, Lazy<Task<Template>>> _templateCache = new();

    public async Task<string> RenderTemplateAsync<TM>(
        string templateName, TM model, CancellationToken cancellationToken = default)
        where TM : IEquatable<TM>, ITemplateRenderModel
    {
        var lazyTemplate = _templateCache.GetOrAdd(templateName, name =>
            new Lazy<Task<Template>>(() => ParseTemplateAsync(name, cancellationToken)));

        var template = await lazyTemplate.Value;
        return await template.RenderAsync(model);
    }

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
