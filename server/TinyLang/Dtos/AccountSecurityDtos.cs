namespace TinyLang.Dtos;

public sealed record SendChangeEmailTokenRequest
{
    public required string NewEmail { get; init; }
}

public sealed record ResetPasswordRequest
{
    public required string NewPassword { get; init; }
    public required string VerificationCode { get; init; }
}

public sealed record ChangeEmailRequest
{
    public required string NewEmail { get; init; }
    public required string VerificationCode { get; init; }
}

public sealed record DeleteAccountRequest
{
    public required string VerificationCode { get; init; }
}

public sealed record ChangeEmailResponse(Guid Id, string Email);
