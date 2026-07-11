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

public sealed class MailKitProvider(IOptions<EmailSettings> options, ILogger<MailKitProvider> logger) : IEmailProvider
{
    private readonly EmailSettings _settings = options.Value;

    public async Task DeliverEmailAsync(SendEmailMessage messageWrapper, CancellationToken cancellationToken = default)
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

    private MimeMessage BuildMimeMessage(SendEmailMessage messageWrapper)
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
