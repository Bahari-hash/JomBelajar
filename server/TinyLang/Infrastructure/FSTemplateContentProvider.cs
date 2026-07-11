using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class FSTemplateContentProvider(
    IHostEnvironment env, ILogger<FSTemplateContentProvider> logger) : ITemplateContentProvider
{
    private readonly string _templateRoot =
        Path.Combine(env.ContentRootPath, "Templates") + Path.DirectorySeparatorChar;

    public async Task<string> GetContentAsync(string templateName, CancellationToken cancellationToken = default)
    {
        var fullPath = Path.GetFullPath(Path.Combine(_templateRoot, templateName));
        if (!fullPath.StartsWith(_templateRoot, StringComparison.OrdinalIgnoreCase))
        {
            logger.LogError("Invalid template path '{TemplateName}'", templateName);
            throw new InvalidOperationException($"Invalid template path '{templateName}'");
        }

        if (!Path.Exists(fullPath))
        {
            logger.LogError("Cannot found template file: '{TemplateName}'", templateName);
            throw new FileNotFoundException($"Cannot found template file '{templateName}'");
        }

        return await File.ReadAllTextAsync(fullPath, cancellationToken);
    }
}
