using System.ComponentModel.DataAnnotations;

namespace TinyLang.Settings;

/// <summary>
/// 描述 JWT 签名、签发方、受众和有效期配置。
/// </summary>
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
    [Range(5, 6 * 60, ErrorMessage = "Jwt access token expiration must between {1} and {2} minutes.")]
    public required int AccessTokenExpMinutes { get; init; }

    [Required(ErrorMessage = "Jwt refresh token expiration time cannot be empty.")]
    [Range(24 * 60, 30 * 24 * 60, ErrorMessage = "Jwt refresh token expiration must between {1} and {2} minutes.")]
    public required int RefreshTokenExpMinutes { get; init; }
}
