namespace TinyLang.Templates;

public sealed record AuthCodeRenderModel : ITemplateRenderModel
{
    public string TemplateName => "auth-code.html";
    public string Subject => $"Hello, this is your auth code.";

    public required string UserEmail { get; init; }
    public required string Code { get; init; }
    public required int ExpiryMinutes { get; init; }
}
