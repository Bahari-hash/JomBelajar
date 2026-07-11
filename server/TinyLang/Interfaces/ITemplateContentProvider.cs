namespace TinyLang.Interfaces;

public interface ITemplateContentProvider
{
    Task<string> GetContentAsync(string templateName, CancellationToken cancellationToken = default);
}
