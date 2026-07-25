using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义用户资料、封禁状态和角色管理的业务契约。
/// </summary>
public interface IUserService
{
    /// <summary>
    /// 更新当前用户的可公开资料字段。
    /// </summary>
    /// <param name="userId">用户标识。</param>
    /// <param name="request">资料更新内容。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>更新后的用户资料。</returns>
    Task<UserProfileResponse> UpdateProfileAsync(
        Guid userId,
        UpdateProfileRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 封禁目标用户并撤销其现有会话。
    /// </summary>
    /// <param name="operatorId">执行封禁的管理员标识。</param>
    /// <param name="targetUserId">目标用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步封禁操作的任务。</returns>
    Task BanAsync(
        Guid operatorId,
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 解除目标用户的封禁状态。
    /// </summary>
    /// <param name="targetUserId">目标用户标识。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>表示异步解封操作的任务。</returns>
    Task UnbanAsync(
        Guid targetUserId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 更新目标用户角色并撤销其现有会话。
    /// </summary>
    /// <param name="targetUserId">目标用户标识。</param>
    /// <param name="request">目标角色。</param>
    /// <param name="cancellationToken">用于取消操作的令牌。</param>
    /// <returns>更新后的角色分配。</returns>
    Task<UserRoleResponse> UpdateRoleAsync(
        Guid targetUserId,
        UpdateRoleRequest request,
        CancellationToken cancellationToken = default);
}
