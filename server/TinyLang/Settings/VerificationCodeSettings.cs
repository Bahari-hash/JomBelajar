using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述验证码长度和有效期配置。
/// </summary>
public sealed record VerificationCodeSettings
{
    public const string SectionName = "VerificationCodeSettings";

    [Range(4, 8, ErrorMessage = "The length of verification code must between {1} and {2}.")]
    public required int CodeLength { get; init; }

    [Range(5, 60, ErrorMessage = "The expiration of verification code must between {1} and {2} minutes.")]
    public required int ExpMinutes { get; init; }
}
