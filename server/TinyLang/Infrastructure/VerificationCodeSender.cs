using Microsoft.Extensions.Options;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;
using TinyLang.Templates;

namespace TinyLang.Infrastructure;

public sealed class VerificationCodeSender(
    IVerificationCodeGenerator codeGenerator,
    IVerificationCodeStore codeStore,
    IOptions<VerificationCodeSettings> options,
    ISecretHasher secretHasher,
    ITemplateRenderer templateRenderer,
    IEmailSender emailSender) : IVerificationCodeSender
{
    private readonly VerificationCodeSettings _settings = options.Value;

    public async Task SendCodeAsync(
        string email, VerificationCodePurpose purpose, CancellationToken cancellationToken = default)
    {
        var code = codeGenerator.GenerateNumeric(_settings.CodeLength);
        var codeHash = secretHasher.Hash(code);
        await codeStore.SaveAsync(email, purpose, codeHash, cancellationToken);

        var templateModel = new VerificationCodeRenderModel
        {
            Code = code,
            UserEmail = email,
            ExpiryMinutes = _settings.ExpMinutes
        };
        var template = await templateRenderer.RenderTemplateAsync(
            templateModel.TemplateName, templateModel, cancellationToken);

        var emailMessage = new EmailMessage
        {
            To = [new EmailAddress { Address = email, Name = email }],
            Subject = templateModel.Subject,
            Body = template,
            IsHtml = true
        };
        await emailSender.EnqueueEmailAsync(emailMessage, cancellationToken);
    }

    public async Task<bool> VerifyCodeAsync(
        string email, VerificationCodePurpose purpose, string code, CancellationToken cancellationToken = default)
    {
        var value = await codeStore.GetAsync(email, purpose, cancellationToken);
        if (value is null || !secretHasher.Verify(code, value))
        {
            return false;
        }

        return await codeStore.TryConsumeAsync(
            email,
            purpose,
            value,
            cancellationToken);
    }
}
