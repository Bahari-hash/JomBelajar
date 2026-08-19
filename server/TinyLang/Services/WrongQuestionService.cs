using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现当前用户错题本的安全查询和带即时判分的单题重做。
/// </summary>
public sealed class WrongQuestionService(
    IApplicationDbContext db,
    TimeProvider timeProvider,
    ILogger<WrongQuestionService> logger) : IWrongQuestionService
{
    public async Task<PagedResponse<PaperWrongQuestionListItemResponse>> GetListAsync(
        Guid userId,
        PaperWrongQuestionListRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(
            request,
            new PaperWrongQuestionListRequestValidator());
        var query = db.PaperWrongQuestions.AsNoTracking().Where(value =>
            value.UserId == userId && value.Status == request.Status);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToLowerInvariant();
            query = query.Where(value =>
                value.Question.Prompt.ToLower().Contains(keyword) ||
                value.Question.Paper!.Title.ToLower().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.LastRedoAt ?? value.LastWrongAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new PaperWrongQuestionListItemResponse(
                value.Id,
                value.QuestionId,
                value.Question.PaperId,
                value.Question.Paper!.Title,
                value.Status,
                value.Question.Type,
                value.Question.Prompt,
                value.Question.AudioResourceId,
                value.WrongCount,
                value.RedoCount,
                value.FirstWrongAt,
                value.LastWrongAt,
                value.LastRedoAt,
                value.MasteredAt,
                value.ConcurrencyStamp))
            .ToListAsync(cancellationToken);
        return new PagedResponse<PaperWrongQuestionListItemResponse>(
            items,
            request.Page,
            request.PageSize,
            totalCount,
            totalCount == 0
                ? 0
                : (totalCount + request.PageSize - 1) / request.PageSize);
    }

    public async Task<PaperWrongQuestionDetailResponse> GetByIdAsync(
        Guid userId,
        Guid wrongQuestionId,
        CancellationToken cancellationToken = default)
        => await db.PaperWrongQuestions.AsNoTracking()
            .Where(value => value.Id == wrongQuestionId && value.UserId == userId)
            .Select(value => new PaperWrongQuestionDetailResponse(
                value.Id,
                value.QuestionId,
                value.Question.PaperId,
                value.Question.Paper!.Title,
                value.Status,
                value.Question.Type,
                value.Question.Prompt,
                value.Question.Points,
                value.Question.Options.OrderBy(option => option.SortOrder)
                    .ThenBy(option => option.Id)
                    .Select(option => new UserPaperAttemptOptionResponse(
                        option.Id,
                        option.Text,
                        option.SortOrder))
                    .ToList(),
                value.Question.AudioResourceId,
                value.Question.DictationBlanks.OrderBy(blank => blank.SortOrder)
                    .ThenBy(blank => blank.Id)
                    .Select(blank => new UserPaperDictationBlankResponse(
                        blank.SortOrder))
                    .ToList(),
                value.WrongCount,
                value.RedoCount,
                value.FirstWrongAt,
                value.LastWrongAt,
                value.LastRedoAt,
                value.MasteredAt,
                value.ConcurrencyStamp))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperWrongQuestionNotFound);

    public async Task<PaperWrongQuestionRedoResponse> RedoAsync(
        Guid userId,
        Guid wrongQuestionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken = default)
    {
        ServiceRequestValidator.Validate(
            request,
            new SavePaperAttemptAnswerRequestValidator());
        try
        {
            return await RedoCoreAsync(
                userId,
                wrongQuestionId,
                request,
                cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ClearTrackedChanges();
            throw ConflictException.Create(
                ErrorCodes.PaperWrongQuestionConcurrencyConflict);
        }
    }

    private async Task<PaperWrongQuestionRedoResponse> RedoCoreAsync(
        Guid userId,
        Guid wrongQuestionId,
        SavePaperAttemptAnswerRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);
        var wrongQuestion = await db.PaperWrongQuestions
            .AsSplitQuery()
            .Include(value => value.Question)
                .ThenInclude(value => value.Paper)
            .Include(value => value.Question)
                .ThenInclude(value => value.Options)
            .Include(value => value.Question)
                .ThenInclude(value => value.AcceptedAnswers)
            .Include(value => value.Question)
                .ThenInclude(value => value.DictationBlanks)
            .SingleOrDefaultAsync(
                value => value.Id == wrongQuestionId && value.UserId == userId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.PaperWrongQuestionNotFound);
        var prepared = PaperAnswerEvaluator.Prepare(wrongQuestion.Question, request);
        var isCorrect = PaperAnswerEvaluator.Score(wrongQuestion.Question, prepared);
        var now = timeProvider.GetUtcNow();

        wrongQuestion.RedoCount++;
        wrongQuestion.LastRedoAt = now;
        wrongQuestion.UpdatedAt = now;
        wrongQuestion.ConcurrencyStamp = Guid.NewGuid();
        if (isCorrect)
        {
            wrongQuestion.Status = PaperWrongQuestionStatus.Mastered;
            wrongQuestion.MasteredAt = now;
        }
        else
        {
            wrongQuestion.Status = PaperWrongQuestionStatus.Pending;
            wrongQuestion.WrongCount++;
            wrongQuestion.LastWrongAt = now;
            wrongQuestion.MasteredAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogInformation(
            "Redid wrong question {WrongQuestionId} for user {UserId}; correct: {IsCorrect}",
            wrongQuestionId,
            userId,
            isCorrect);
        return ToRedoResponse(wrongQuestion, prepared, isCorrect);
    }

    private static PaperWrongQuestionRedoResponse ToRedoResponse(
        PaperWrongQuestion wrongQuestion,
        PreparedPaperAnswer prepared,
        bool isCorrect)
    {
        var question = wrongQuestion.Question;
        return new PaperWrongQuestionRedoResponse(
            wrongQuestion.Id,
            wrongQuestion.QuestionId,
            wrongQuestion.Status,
            question.Type,
            isCorrect,
            question.Explanation,
            prepared.SelectedOptionId,
            prepared.BooleanAnswer,
            prepared.TextAnswer,
            prepared.TextAnswers,
            question.Options.Where(value => value.IsCorrect)
                .Select(value => (Guid?)value.Id)
                .SingleOrDefault(),
            question.CorrectBoolean,
            question.AcceptedAnswers.OrderBy(value => value.SortOrder)
                .ThenBy(value => value.Id)
                .Select(value => value.Text)
                .ToArray(),
            question.DictationBlanks.OrderBy(value => value.SortOrder)
                .ThenBy(value => value.Id)
                .Select(value => value.Answer)
                .ToArray(),
            wrongQuestion.WrongCount,
            wrongQuestion.RedoCount,
            wrongQuestion.LastWrongAt,
            wrongQuestion.LastRedoAt!.Value,
            wrongQuestion.MasteredAt,
            wrongQuestion.ConcurrencyStamp);
    }
}
