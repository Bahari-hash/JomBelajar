using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.Services;

/// <summary>
/// 定义登录用户学习新词和复习到期单词的业务用例。
/// </summary>
public interface IWordStudyService
{
    Task<WordStudySessionStateResponse> FinishSummaryAsync(Guid userId, Guid sessionId,
        WordStudySessionType type, bool skipSpelling, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> ContinueWaitingSessionAsync(
        Guid userId, Guid sessionId, WordStudySessionType type, bool addNewWords,
        CancellationToken cancellationToken = default);
    Task<WordLearningOverviewResponse> GetLearningOverviewAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> StartLearningSessionAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> GetLearningSessionAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WordStudyCompletedItemResponse>> GetCompletedSessionItemsAsync(
        Guid userId,
        Guid sessionId,
        WordStudySessionType expectedType,
        CancellationToken cancellationToken = default);
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
    Task<WordStudyCommandResponse> SubmitLearningSpellingAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordSpellingRequest request,
        CancellationToken cancellationToken = default);
    Task<WordStudyCommandResponse> SubmitReviewSpellingAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        SubmitWordSpellingRequest request,
        CancellationToken cancellationToken = default);
    Task<WordReviewOverviewResponse> GetReviewOverviewAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> StartReviewSessionAsync(
        Guid userId, CancellationToken cancellationToken = default);
    Task<WordStudySessionStateResponse> GetReviewSessionAsync(
        Guid userId, Guid sessionId, CancellationToken cancellationToken = default);
    Task<WordStudyCommandResponse> ExcludeFromReviewAsync(
        Guid userId,
        Guid sessionId,
        Guid itemId,
        ExcludeWordFromReviewRequest request,
        CancellationToken cancellationToken = default);
    Task<WordStudyTodayReviewResponse> GetTodayReviewAsync(
        Guid userId,
        WordStudyTodayReviewRequest request,
        CancellationToken cancellationToken = default);
    Task<WordStudyCheckInCalendarResponse> GetCheckInCalendarAsync(
        Guid userId,
        WordStudyCheckInCalendarRequest request,
        CancellationToken cancellationToken = default);
}
