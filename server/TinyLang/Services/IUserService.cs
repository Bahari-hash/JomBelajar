using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义用户资料查询、管理员列表、封禁状态和角色管理的业务契约。
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 获取当前登录用户可见的私有账户资料。
    /// </summary>
    Task<CurrentUserProfileResponse> GetCurrentProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取未封禁且未删除用户的最小公开资料。
    /// </summary>
    Task<PublicUserProfileResponse> GetPublicProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员可见的用户筛选和分页列表。
    /// </summary>
    Task<PagedResponse<AdminUserListItemResponse>> GetAdminListAsync(
        AdminUserListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取管理员可见的单个用户管理详情。
    /// </summary>
    Task<AdminUserDetailResponse> GetAdminByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新当前用户的可公开资料字段。
    /// </summary>
    Task<CurrentUserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 使用必填原因封禁目标用户并原子撤销其现有会话。
    /// </summary>
    Task BanAsync(
        Guid operatorId,
        Guid targetUserId,
        BanUserRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等解除目标用户的封禁状态。
    /// </summary>
    Task UnbanAsync(
        Guid operatorId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新目标用户角色并在实际变化时原子撤销其现有会话。
    /// </summary>
    Task<UserRoleResponse> UpdateRoleAsync(
        Guid operatorId,
        Guid targetUserId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default);
}
