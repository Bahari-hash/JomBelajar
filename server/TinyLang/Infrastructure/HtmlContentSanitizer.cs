using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using Ganss.Xss;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Infrastructure;

public sealed class HtmlContentSanitizer : IHtmlContentSanitizer
{
    private static readonly string[] AllowedTags =
    [
        "p", "br", "strong", "em", "u", "s", "blockquote",
        "h1", "h2", "h3", "h4", "h5", "h6",
        "ul", "ol", "li", "pre", "code", "a", "img"
    ];

    private static readonly string[] AllowedAttributes =
    [
        "href", "src", "alt", "title"
    ];

    public HtmlSanitizationResult Sanitize(string html)
    {
        if (string.IsNullOrWhiteSpace(html))
        {
            return new HtmlSanitizationResult(string.Empty, string.Empty, [], false);
        }

        var parser = new HtmlParser();
        var inputDocument = parser.ParseDocument(html);
        var hasInvalidUrls = ContainsInvalidUrls(inputDocument);

        var sanitizer = CreateSanitizer();
        var sanitizedHtml = sanitizer.Sanitize(html);
        var sanitizedDocument = parser.ParseDocument(sanitizedHtml);
        var body = sanitizedDocument.Body;
        if (body is null)
        {
            return new HtmlSanitizationResult(string.Empty, string.Empty, [], hasInvalidUrls);
        }

        foreach (var element in body.QuerySelectorAll("*"))
        {
            foreach (var attribute in element.Attributes.ToArray())
            {
                if (!IsAllowedAttribute(element.LocalName, attribute.LocalName))
                {
                    element.RemoveAttribute(attribute.LocalName);
                }
            }
        }

        foreach (var element in body.QuerySelectorAll("a[href], img[src]"))
        {
            var attributeName = element.LocalName == "a" ? "href" : "src";
            var value = element.GetAttribute(attributeName);
            if (IsAllowedAbsoluteUrl(value))
            {
                continue;
            }

            element.RemoveAttribute(attributeName);
            hasInvalidUrls = true;
        }

        var imageSources = body.QuerySelectorAll("img")
            .Select(element => element.GetAttribute("src"))
            .Where(source => source is not null)
            .Select(source => source!)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (body.QuerySelectorAll("img").Any(element => element.GetAttribute("src") is null))
        {
            hasInvalidUrls = true;
        }

        return new HtmlSanitizationResult(
            body.InnerHtml,
            body.TextContent.Trim(),
            imageSources,
            hasInvalidUrls);
    }

    private static HtmlSanitizer CreateSanitizer()
    {
        var sanitizer = new HtmlSanitizer();
        sanitizer.AllowedTags.Clear();
        sanitizer.AllowedTags.UnionWith(AllowedTags);
        sanitizer.AllowedAttributes.Clear();
        sanitizer.AllowedAttributes.UnionWith(AllowedAttributes);
        sanitizer.AllowedSchemes.Clear();
        sanitizer.AllowedSchemes.Add(Uri.UriSchemeHttp);
        sanitizer.AllowedSchemes.Add(Uri.UriSchemeHttps);
        sanitizer.AllowDataAttributes = false;
        return sanitizer;
    }

    private static bool ContainsInvalidUrls(IParentNode document)
    {
        foreach (var element in document.QuerySelectorAll("a[href], img"))
        {
            var attributeName = element.LocalName == "a" ? "href" : "src";
            if (!IsAllowedAbsoluteUrl(element.GetAttribute(attributeName)))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsAllowedAbsoluteUrl(string? value)
        => Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);

    private static bool IsAllowedAttribute(string tagName, string attributeName)
        => tagName switch
        {
            "a" => attributeName is "href" or "title",
            "img" => attributeName is "src" or "alt" or "title",
            _ => false
        };
}
