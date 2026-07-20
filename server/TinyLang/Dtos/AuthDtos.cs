using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

public sealed record RegisterTokenRequest
{
    public required string Email { get; init; }
}

public sealed record RegisterRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
    public required string VerificationCode { get; init; }
}

public sealed record LoginRequest
{
    public required string Email { get; init; }
    public required string Password { get; init; }
}

public sealed record RefreshTokenRequest
{
    public required string RefreshToken { get; init; }
}

public sealed record LogoutRequest
{
    public required string RefreshToken { get; init; }
}

public sealed record UserResponse(Guid Id, string Email, UserRole Role);

public sealed record AuthTokenResponse(
    string Token,
    string RefreshToken,
    long ExpiresIn,
    UserResponse User);
