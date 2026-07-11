namespace TinyLang.Templates;

public sealed record VerificationCodeRenderModel : ITemplateRenderModel
{
    public string TemplateName => "verification-code.html";
    public string Subject => "Hello, this is your verification code.";

    public required string UserEmail { get; init; }
    public required string Code { get; init; }
    public required int ExpiryMinutes { get; init; }
}
