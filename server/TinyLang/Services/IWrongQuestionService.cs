using TinyLang.Dtos;

namespace TinyLang.Services;

/// <summary>
/// 定义当前用户查询和重做个人错题的用例。
/// </summary>
public interface IWrongQuestionService
{
    Task<PagedResponse<PaperWrongQuestionListItemResponse>> GetListAsync(
        Guid userId,
        PaperWrongQuestionListRequest request,
        CancellationToken cancellationToken = default);

    Task<PaperWrongQuestionDetailResponse> GetByIdAsync(
        Guid userId,
        Guid wrongQuestionId,
        CancellationToken cancellationToken = default);

    Task<PaperWrongQuestionRedoResponse> RedoAsync(
        Guid userId,
        Guid wrongQuestionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken = default);
}
