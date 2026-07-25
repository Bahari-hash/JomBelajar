using System.IO;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

/// <summary>
/// 从应用内容根目录下的 Templates 目录安全读取模板文件。
/// </summary>
/// <param name="env">主机环境和内容根目录。</param>
/// <param name="logger">模板文件访问日志记录器。</param>
public sealed class TemplateContentProvider(
    IHostEnvironment env, ILogger<TemplateContentProvider> logger) : ITemplateContentProvider
{
    private readonly string _templateRoot =
        Path.Combine(env.ContentRootPath, "Templates") + Path.DirectorySeparatorChar;

    /// <inheritdoc />
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
