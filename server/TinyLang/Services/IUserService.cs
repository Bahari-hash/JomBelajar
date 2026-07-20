using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IUserService
{
    Task<UserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    Task BanAsync(
        Guid operatorId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    Task UnbanAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    Task<UserRoleResponse> UpdateRoleAsync(
        Guid targetUserId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default);
}
