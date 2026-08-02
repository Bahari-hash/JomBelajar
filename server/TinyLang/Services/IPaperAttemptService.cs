using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义登录用户开始、恢复、作答、提交和查询试卷测验的用例。
/// </summary>
public interface IPaperAttemptService
{
    /// <summary>
    /// 创建新的活动测验或恢复当前用户对同一试卷已有的活动测验。
    /// </summary>
    Task<PaperAttemptStartOutcome> StartAsync(
        Guid userId,
        Guid paperId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前用户拥有的可恢复测验内容和已保存答案。
    /// </summary>
    Task<UserPaperAttemptResponse> GetAttemptAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前用户针对指定试卷的有界测验历史。
    /// </summary>
    Task<PagedResponse<PaperAttemptSummaryResponse>> GetHistoryAsync(
        Guid userId,
        Guid paperId,
        PaperAttemptListRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 在活动测验中新增、覆盖或幂等保存一道题的答案。
    /// </summary>
    Task SaveAnswerAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 幂等清除活动测验中一道属于当前试卷的已保存答案。
    /// </summary>
    Task ClearAnswerAsync(
        Guid userId,
        Guid attemptId,
        Guid questionId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 原子判分并提交测验，重复提交返回已经持久化的相同结果。
    /// </summary>
    Task<PaperAttemptResultResponse> SubmitAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取当前用户已经提交的稳定逐题测验结果。
    /// </summary>
    Task<PaperAttemptResultResponse> GetResultAsync(
        Guid userId,
        Guid attemptId,
        CancellationToken cancellationToken = default);
}
