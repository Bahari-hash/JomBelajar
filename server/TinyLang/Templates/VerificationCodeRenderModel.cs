namespace TinyLang.Templates;

/// <summary>
/// 提供验证码邮件模板渲染所需的数据。
/// </summary>
public sealed record VerificationCodeRenderModel : ITemplateRenderModel
{
    public string TemplateName => "verification-code.html";
    public string Subject => "Hello, this is your verification code.";

    public required string UserEmail { get; init; }
    public required string Code { get; init; }
    public required int ExpiryMinutes { get; init; }
}
