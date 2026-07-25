using System.ComponentModel.DataAnnotations;
using TinyLang.Enums;

namespace TinyLang.Settings;

/// <summary>
/// 描述 SMTP 连接、认证及默认发件人配置。
/// </summary>
public sealed record EmailSettings
{
    public const string SectionName = "EmailSettings";

    [Required(ErrorMessage = "SMTP server host cannot be empty.")]
    public required string SmtpServer { get; init; }

    [Required(ErrorMessage = "SMTP server port cannot be empty.")]
    [Range(1, 65535, ErrorMessage = "SMTP server port must between {1} and {2}.")]
    public required int SmtpPort { get; init; }

    [Required(ErrorMessage = "SMTP secure mode cannot be empty.")]
    [EnumDataType(typeof(SmtpSecureMode), ErrorMessage = "Invalid SMTP secure mode.")]
    public required SmtpSecureMode SecureMode { get; init; }

    [Required(ErrorMessage = "Email sender's name cannot be empty.")]
    [MaxLength(64, ErrorMessage = "Email sender name cannot exceed {1} characters.")]
    public required string SenderName { get; init; }

    [Required(ErrorMessage = "Email sender's email address cannot be empty.")]
    public required string SenderEmail { get; init; }

    [Required(ErrorMessage = "Username of smtp server cannot be empty.")]
    public required string Username { get; init; }

    [Required(ErrorMessage = "Password of smtp server cannot be empty.")]
    public required string Password { get; init; }
}
