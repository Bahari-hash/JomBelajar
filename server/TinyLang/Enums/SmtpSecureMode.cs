namespace TinyLang.Enums;

/// <summary>
/// 指定 SMTP 连接采用的传输安全协商方式。
/// </summary>
public enum SmtpSecureMode
{
    None,
    Auto,
    SslOnConnect,
    StartTls,
    StartTlsWhenAvailable
}
