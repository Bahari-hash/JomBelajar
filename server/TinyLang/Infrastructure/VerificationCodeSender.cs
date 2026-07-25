using Microsoft.Extensions.Options;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;
using TinyLang.Templates;

namespace TinyLang.Infrastructure;

/// <summary>
/// 编排验证码生成、散列存储、模板渲染和邮件排队发送。
/// </summary>
/// <param name="codeGenerator">密码学安全验证码生成器。</param>
/// <param name="codeStore">验证码临时存储。</param>
/// <param name="options">验证码长度和有效期配置。</param>
/// <param name="secretHasher">验证码散列和验证服务。</param>
/// <param name="templateRenderer">邮件模板渲染器。</param>
/// <param name="emailSender">邮件发送调度服务。</param>
public sealed class VerificationCodeSender(
    IVerificationCodeGenerator codeGenerator,
    IVerificationCodeStore codeStore,
    IOptions<VerificationCodeSettings> options,
    ISecretHasher secretHasher,
    ITemplateRenderer templateRenderer,
    IEmailSender emailSender) : IVerificationCodeSender
{
    private readonly VerificationCodeSettings _settings = options.Value;

    /// <inheritdoc />
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

    /// <inheritdoc />
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
