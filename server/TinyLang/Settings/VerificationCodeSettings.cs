using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

public sealed record VerificationCodeSettings
{
    public const string SectionName = "VerificationCodeSettings";

    [Range(4, 8, ErrorMessage = "The length of verification code must between {1} and {2}.")]
    public required int CodeLength { get; init; }

    [Range(5, 60, ErrorMessage = "The expiration of verification code must between {1} and {2} minutes.")]
    public required int ExpMinutes { get; init; }
}
