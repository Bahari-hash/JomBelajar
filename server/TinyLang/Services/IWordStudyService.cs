using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义登录用户创建、恢复和完成基础单词背诵会话的业务用例。
/// </summary>
public interface IWordStudyService
{
    Task<WordLearningOverviewResponse> GetLearningOverviewAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> StartLearningSessionAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> GetLearningSessionAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WordStudyCompletedItemResponse>> GetCompletedSessionItemsAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<WordStudyCommandResponse> SubmitLearningMemorizationAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordMemorizationRequest request,
        CancellationToken cancellationToken = default);
    Task<WordStudyCommandResponse> SubmitReviewMemorizationAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordMemorizationRequest request,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 获取当前 UTC 日期的每日背诵状态。
    /// </summary>
    Task<WordStudyTodayResponse> GetTodayAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等创建或恢复当前 UTC 日期的每日背诵会话。
    /// </summary>
    Task<WordStudySessionResponse> StartTodayAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// 按指定数量、历史范围和选词模式创建固定内容的活动会话。
    /// </summary>
    Task<WordStudySessionResponse> CreateSessionAsync(
        Guid userId,
        CreateWordStudySessionRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前用户唯一的活动会话；不存在时返回 null。
    /// </summary>
    Task<WordStudySessionResponse?> GetActiveSessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前用户拥有的指定会话摘要。
    /// </summary>
    Task<WordStudySessionResponse> GetSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按固定顺序返回当前用户指定会话中的全部项目及实时可见内容。
    /// </summary>
    Task<IReadOnlyList<WordStudySessionItemResponse>> GetSessionItemsAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取 Position 最小的可用待处理项，并跳过实时不可见内容。
    /// </summary>
    Task<WordStudyNextItemResponse?> GetNextItemAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等提交会话项结果并原子累计当前用户的词条进度。
    /// </summary>
    Task<WordStudySessionResponse> SubmitResultAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordStudyResultRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等放弃当前用户仍处于活动状态的指定会话。
    /// </summary>
    Task<WordStudySessionResponse> AbandonSessionAsync(
        Guid userId,
        Guid sessionId,
        CancellationToken cancellationToken = default);
}
