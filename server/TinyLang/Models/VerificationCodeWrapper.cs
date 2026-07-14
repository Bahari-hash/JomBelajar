using TinyLang.Enums;

namespace TinyLang.Models;

public sealed record VerificationCodeWrapper
{
    public required Guid Id { get; init; }
    public required VerificationCode Code { get; init; }
    public required DateTimeOffset CreateTime { get; init; }
}
