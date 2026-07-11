using TinyLang.Templates;

namespace TinyLang.Interfaces;

public interface ITemplateRenderer
{
    Task<string> RenderTemplateAsync<TM>(
        string templateName, TM model, CancellationToken cancellationToken = default)
        where TM : IEquatable<TM>, ITemplateRenderModel;
}
