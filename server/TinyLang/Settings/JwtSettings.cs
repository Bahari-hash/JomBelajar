using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

public sealed record JwtSettings
{
    public const string SectionName = nameof(JwtSettings);

    [Required(ErrorMessage = "Jwt secret cannot be empty.")]
    [MinLength(32, ErrorMessage = "Jwt secret must be more than {1} characters.")]
    public required string JwtSecret { get; init; }

    [Required(ErrorMessage = "Jwt issuer cannot be empty.")]
    public required string Issuer { get; init; }

    [Required(ErrorMessage = "Jwt audience cannot be empty.")]
    public required string Audience { get; init; }

    [Required(ErrorMessage = "Jwt access token expiration time cannot be empty.")]
    public required int AccessTokenExpMinutes { get; init; }

    [Required(ErrorMessage = "Jwt refresh token expiration time cannot be empty.")]
    public required int RefreshTokenExpMinutes { get; init; }
}
