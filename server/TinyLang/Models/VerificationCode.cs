using TinyLang.Enums;

namespace TinyLang.Models;

public sealed record VerificationCode
{
    public required string Code { get; init; }
    public required string Email { get; init; }
    public required VerificationCodePurpose Purpose { get; init; }
}
