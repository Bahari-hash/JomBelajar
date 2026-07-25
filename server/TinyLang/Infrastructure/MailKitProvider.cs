using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;
using TinyLang.Enums;
using TinyLang.Interfaces;
using TinyLang.Models;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 MailKit 将应用邮件转换为 MIME 消息并通过 SMTP 投递。
/// </summary>
/// <param name="options">SMTP 和默认发件人配置。</param>
/// <param name="logger">邮件投递日志记录器。</param>
public sealed class MailKitProvider(IOptions<EmailSettings> options, ILogger<MailKitProvider> logger) : IEmailProvider
{
    private readonly EmailSettings _settings = options.Value;

    /// <inheritdoc />
    public async Task DeliverEmailAsync(EmailMessageWrapper messageWrapper, CancellationToken cancellationToken = default)
    {
        var message = messageWrapper.Message;
        if (message.To.Count == 0)
        {
            throw new InvalidOperationException("Email receiver cannot be empty.");
        }

        var mime = BuildMimeMessage(messageWrapper);

        using var client = new SmtpClient();
        var secureOption = ResolveSmtpSecureMode();
        await client.ConnectAsync(_settings.SmtpServer, _settings.SmtpPort, secureOption, cancellationToken);

        try
        {
            await client.AuthenticateAsync(_settings.Username, _settings.Password, cancellationToken);
            await client.SendAsync(mime, cancellationToken);
            logger.LogInformation("Email was sended successfully.");
        }
        catch (Exception ex)
        {
            await client.DisconnectAsync(true, cancellationToken);
            logger.LogWarning(ex, "Failed to send email with id {MessageId}.", messageWrapper.Id);
        }
    }

    /// <summary>
    /// 将应用邮件、地址和附件转换为 MimeKit 消息。
    /// </summary>
    /// <param name="messageWrapper">包含稳定消息标识的应用邮件。</param>
    /// <returns>可交给 SMTP client 的 MIME 消息。</returns>
    private MimeMessage BuildMimeMessage(EmailMessageWrapper messageWrapper)
    {
        var mime = new MimeMessage
        {
            MessageId = messageWrapper.Id.ToString()
        };

        var message = messageWrapper.Message;
        mime.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));

        FillAddress(message.To, mime.To);
        FillAddress(message.Cc, mime.Cc);
        FillAddress(message.Bcc, mime.Bcc);

        mime.Subject = message.Subject;
        var builder = new BodyBuilder();
        if (message.IsHtml)
        {
            builder.HtmlBody = message.Body;
        }
        else
        {
            builder.TextBody = message.Body;
        }

        foreach (var attachment in message.EmailAttachments)
        {
            builder.Attachments.Add(
                attachment.FileName,
                attachment.Content,
                ContentType.Parse(attachment.ContentType));
        }

        mime.Body = builder.ToMessageBody();
        return mime;

        static void FillAddress(IEnumerable<EmailAddress> source, InternetAddressList target)
        {
            foreach (var addr in source)
            {
                var mailBoxAddress = new MailboxAddress(addr.Name, addr.Address);
                target.Add(mailBoxAddress);
            }
        }
    }

    /// <summary>
    /// 将应用安全模式映射为 MailKit socket 选项，并为 Auto 模式按端口选择默认值。
    /// </summary>
    /// <returns>SMTP 连接使用的安全选项。</returns>
    private SecureSocketOptions ResolveSmtpSecureMode()
    {
        return _settings.SecureMode switch
        {
            SmtpSecureMode.None => SecureSocketOptions.None,
            SmtpSecureMode.SslOnConnect => SecureSocketOptions.SslOnConnect,
            SmtpSecureMode.StartTls => SecureSocketOptions.StartTls,
            SmtpSecureMode.StartTlsWhenAvailable => SecureSocketOptions.StartTlsWhenAvailable,
            _ => _settings.SmtpPort switch
            {
                465 => SecureSocketOptions.SslOnConnect,
                587 => SecureSocketOptions.StartTls,
                _ => SecureSocketOptions.Auto
            }
        };
    }
}
