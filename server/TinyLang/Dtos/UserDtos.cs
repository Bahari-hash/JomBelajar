using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

public sealed record UpdateProfileRequest
{
    public string? Nickname { get; init; }
    public string? AvatarUrl { get; init; }
    public string? Bio { get; init; }
}

public sealed record UpdateRoleRequest
{
    public required string Role { get; init; }
}

public sealed record UserProfileResponse(
    Guid Id,
    string Email,
    UserRole Role,
    string? Nickname,
    string? AvatarUrl,
    string? Bio,
    bool IsBanned);

public sealed record UserRoleResponse(Guid UserId, UserRole Role);
