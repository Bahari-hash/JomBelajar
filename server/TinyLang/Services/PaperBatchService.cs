using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 负责试卷批量 JSON 的完整校验和整批事务导入。
/// </summary>
public sealed class PaperBatchService(
    IApplicationDbContext db,
    IPaperService paperService,
    IValidator<CreatePaperRequest> paperValidator) : IPaperBatchService
{
    public async Task<PaperBatchValidationResponse> ValidateAsync(
        PaperBatchRequest request,
        CancellationToken cancellationToken = default)
        => (await BuildAsync(request, cancellationToken)).Response;

    public async Task<PaperBatchImportResult> ImportAsync(
        Guid adminId,
        PaperBatchRequest request,
        CancellationToken cancellationToken = default)
    {
        var prepared = await BuildAsync(request, cancellationToken);
        if (!prepared.Response.IsValid)
        {
            return new PaperBatchImportResult(null, prepared.Response);
        }

        try
        {
            await using var transaction = await db.BeginTransactionAsync(cancellationToken);
            var created = new List<PaperBatchCreatedItemResponse>();
            foreach (var item in prepared.Items)
            {
                var paper = await paperService.CreateDraftAsync(
                    adminId,
                    item.Request,
                    cancellationToken);
                created.Add(new PaperBatchCreatedItemResponse(item.PaperIndex, paper.Id));
            }
            await transaction.CommitAsync(cancellationToken);
            return new PaperBatchImportResult(
                new PaperBatchImportResponse(created.Count, created), null);
        }
        catch (DbUpdateException)
        {
            return await CreateConflictResultAsync(request, cancellationToken);
        }
        catch (BaseAppException exception) when (IsReferenceConflict(exception.ErrorCode))
        {
            return await CreateConflictResultAsync(request, cancellationToken);
        }
    }

    private async Task<BuildResult> BuildAsync(
        PaperBatchRequest? request,
        CancellationToken cancellationToken)
    {
        var papers = request?.Papers;
        if (papers is null || papers.Count == 0)
        {
            var empty = new PaperBatchSummaryResponse(0, 0, 0, 0, 0, 0, 0, 0);
            return new BuildResult(
                new PaperBatchValidationResponse(false, empty, [],
                [Issue(null, null, "papers", ErrorCodes.PaperBatchRequired)]), []);
        }

        var paperArray = papers.ToArray();
        if (paperArray.Any(value => value is null))
        {
            var invalid = new PaperBatchSummaryResponse(
                paperArray.Length, 0, 0, 0, 0, 0, 0, 0);
            return new BuildResult(
                new PaperBatchValidationResponse(false, invalid, [],
                [Issue(null, null, "papers", ErrorCodes.PaperQuestionCollectionInvalid)]), []);
        }
        var summary = Summarize(paperArray);
        if (paperArray.Length > OnlineQuizConstraints.MaxBatchPaperCount)
        {
            return new BuildResult(
                new PaperBatchValidationResponse(false, summary, [],
                [Issue(null, null, "papers", ErrorCodes.PaperBatchCountLimit)]), []);
        }

        var categoryNames = paperArray.SelectMany(x => x.CategoryNames ?? [])
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(NormalizeName).Distinct(StringComparer.Ordinal).ToArray();
        var categories = await db.PaperCategories.AsNoTracking()
            .Where(x => categoryNames.Contains(x.Name))
            .ToListAsync(cancellationToken);
        var categoryLookup = categories.ToDictionary(x => NormalizeName(x.Name), StringComparer.Ordinal);

        var audioNames = paperArray.SelectMany(x => x.Questions ?? [])
            .Where(x => x is not null && x.Type == PaperQuestionType.Dictation && !string.IsNullOrWhiteSpace(x.AudioFileName))
            .Select(x => NormalizeAudioName(x.AudioFileName!)).Distinct(StringComparer.Ordinal).ToArray();
        var audioResources = await db.AudioResources.AsNoTracking()
            .Where(x => audioNames.Contains(x.NormalizedName))
            .ToListAsync(cancellationToken);
        var audioLookup = audioResources.ToDictionary(x => x.NormalizedName, StringComparer.Ordinal);

        var errors = new List<PaperBatchValidationErrorResponse>();
        var previews = new List<PaperBatchPaperValidationResponse>(paperArray.Length);
        var items = new List<PreparedItem>(paperArray.Length);
        var matchedCategoryReferenceCount = 0;
        var matchedAudioReferenceCount = 0;
        for (var paperIndex = 0; paperIndex < paperArray.Length; paperIndex++)
        {
            var source = paperArray[paperIndex];
            var paperErrorCount = errors.Count;
            var categoryIds = new List<Guid>();
            var seenCategoryNames = new HashSet<string>(StringComparer.Ordinal);
            foreach (var categoryName in source.CategoryNames ?? [])
            {
                if (string.IsNullOrWhiteSpace(categoryName))
                {
                    errors.Add(Issue(paperIndex, null, $"papers[{paperIndex}].categoryNames", ErrorCodes.PaperBatchCategoryNotFound));
                    continue;
                }
                var normalizedCategoryName = NormalizeName(categoryName);
                if (!seenCategoryNames.Add(normalizedCategoryName))
                {
                    errors.Add(Issue(paperIndex, null, $"papers[{paperIndex}].categoryNames", ErrorCodes.PaperCategoryDuplicate));
                }
                else if (!categoryLookup.TryGetValue(normalizedCategoryName, out var category))
                {
                    errors.Add(Issue(paperIndex, null, $"papers[{paperIndex}].categoryNames", ErrorCodes.PaperBatchCategoryNotFound));
                }
                else if (!category.IsActive)
                {
                    errors.Add(Issue(paperIndex, null, $"papers[{paperIndex}].categoryNames", ErrorCodes.PaperBatchCategoryInactive));
                }
                else if (!categoryIds.Contains(category.Id))
                {
                    categoryIds.Add(category.Id);
                    matchedCategoryReferenceCount++;
                }
            }

            var questions = new List<PaperQuestionInput>();
            var sourceQuestions = source.Questions?.ToArray() ?? [];
            for (var questionIndex = 0; questionIndex < sourceQuestions.Length; questionIndex++)
            {
                var sourceQuestion = sourceQuestions[questionIndex];
                if (sourceQuestion is null)
                {
                    errors.Add(Issue(paperIndex, questionIndex,
                        $"papers[{paperIndex}].questions[{questionIndex}]",
                        ErrorCodes.PaperQuestionCollectionInvalid));
                    continue;
                }
                Guid? audioId = null;
                if (sourceQuestion.Type == PaperQuestionType.Dictation)
                {
                    if (string.IsNullOrWhiteSpace(sourceQuestion.AudioFileName))
                    {
                        errors.Add(Issue(paperIndex, questionIndex, $"papers[{paperIndex}].questions[{questionIndex}].audioFileName", ErrorCodes.AudioNotFound));
                    }
                    else if (!audioLookup.TryGetValue(NormalizeAudioName(sourceQuestion.AudioFileName), out var audio))
                    {
                        errors.Add(Issue(paperIndex, questionIndex, $"papers[{paperIndex}].questions[{questionIndex}].audioFileName", ErrorCodes.PaperBatchAudioNotFound));
                    }
                    else if (audio.Status != AudioResourceStatus.Ready)
                    {
                        errors.Add(Issue(paperIndex, questionIndex, $"papers[{paperIndex}].questions[{questionIndex}].audioFileName", ErrorCodes.PaperBatchAudioNotReady));
                    }
                    else
                    {
                        audioId = audio.Id;
                        matchedAudioReferenceCount++;
                    }
                }
                else if (!string.IsNullOrWhiteSpace(sourceQuestion.AudioFileName) || (sourceQuestion.Blanks?.Count ?? 0) > 0)
                {
                    errors.Add(Issue(paperIndex, questionIndex, $"papers[{paperIndex}].questions[{questionIndex}]", ErrorCodes.PaperQuestionShapeInvalid));
                }

                questions.Add(new PaperQuestionInput
                {
                    Type = sourceQuestion.Type,
                    Prompt = sourceQuestion.Prompt,
                    Explanation = sourceQuestion.Explanation,
                    Points = sourceQuestion.Points,
                    SortOrder = sourceQuestion.SortOrder,
                    CorrectBoolean = sourceQuestion.CorrectBoolean,
                    FillBlankCaseSensitive = sourceQuestion.FillBlankCaseSensitive,
                    Options = sourceQuestion.Options ?? [],
                    AcceptedAnswers = sourceQuestion.AcceptedAnswers ?? [],
                    AudioResourceId = audioId,
                    DictationBlanks = sourceQuestion.Blanks ?? []
                });
            }

            var createRequest = new CreatePaperRequest
            {
                Title = source.Title,
                Description = source.Description,
                Instructions = source.Instructions,
                CategoryIds = categoryIds,
                PassingScorePercentage = source.PassingScorePercentage,
                Questions = questions
            };
            var validation = paperValidator.Validate(createRequest);
            foreach (var failure in validation.Errors)
            {
                var code = Enum.TryParse<ErrorCodes>(failure.ErrorCode, out var parsed)
                    ? parsed : ErrorCodes.RequestValidationFailed;
                errors.Add(Issue(paperIndex, QuestionIndex(failure.PropertyName),
                    $"papers[{paperIndex}].{ToJsonFieldPath(failure.PropertyName)}", code));
            }
            previews.Add(new PaperBatchPaperValidationResponse(
                paperIndex,
                source.Title,
                errors.Count == paperErrorCount,
                questions.Count,
                source.CategoryNames?.ToArray() ?? [],
                categoryIds.Count,
                source.Questions?.Where(x => x is not null &&
                    !string.IsNullOrWhiteSpace(x.AudioFileName))
                    .Select(x => x.AudioFileName!).ToArray() ?? [],
                questions.Count(question => question.AudioResourceId.HasValue)));
            items.Add(new PreparedItem(paperIndex, createRequest));
        }

        summary = summary with
        {
            MatchedCategoryReferenceCount = matchedCategoryReferenceCount,
            MatchedAudioReferenceCount = matchedAudioReferenceCount
        };
        var response = new PaperBatchValidationResponse(
            errors.Count == 0, summary, previews, errors);
        return new BuildResult(response, errors.Count == 0 ? items : []);
    }

    private static PaperBatchSummaryResponse Summarize(IReadOnlyCollection<PaperBatchItemRequest> papers)
    {
        var questions = papers.SelectMany(x => x.Questions ?? [])
            .Where(x => x is not null).ToArray();
        return new PaperBatchSummaryResponse(
            papers.Count,
            questions.Length,
            questions.Count(x => x.Type == PaperQuestionType.Dictation),
            questions.Where(x => x.Type == PaperQuestionType.Dictation).Sum(x => x.Blanks?.Count ?? 0),
            papers.Sum(x => x.CategoryNames?.Count ?? 0),
            0,
            questions.Count(x => x.Type == PaperQuestionType.Dictation && !string.IsNullOrWhiteSpace(x.AudioFileName)),
            0);
    }

    private async Task<PaperBatchImportResult> CreateConflictResultAsync(
        PaperBatchRequest request,
        CancellationToken cancellationToken)
    {
        db.ClearTrackedChanges();
        var refreshed = await BuildAsync(request, cancellationToken);
        var error = new PaperBatchValidationErrorResponse(
            null, null, "papers", ErrorCodes.PaperBatchConflict,
            ErrorCodes.PaperBatchConflict.GetMessage());
        return new PaperBatchImportResult(null, refreshed.Response with
        {
            IsValid = false,
            Errors = refreshed.Response.Errors.Append(error).ToArray()
        });
    }

    private static bool IsReferenceConflict(ErrorCodes errorCode)
        => errorCode is ErrorCodes.PaperCategoryNotFound or
            ErrorCodes.PaperCategoryInactive or
            ErrorCodes.AudioNotFound or
            ErrorCodes.AudioNotReady;

    private static string ToJsonFieldPath(string propertyName)
        => propertyName
            .Replace(".Answer", ".answer", StringComparison.Ordinal)
            .Replace(".Text", ".text", StringComparison.Ordinal)
            .Replace("DictationBlanks", "blanks", StringComparison.Ordinal)
            .Replace("AcceptedAnswers", "acceptedAnswers", StringComparison.Ordinal)
            .Replace("CategoryIds", "categoryNames", StringComparison.Ordinal)
            .Replace("Questions", "questions", StringComparison.Ordinal)
            .Replace("Options", "options", StringComparison.Ordinal)
            .Replace("Prompt", "prompt", StringComparison.Ordinal)
            .Replace("Explanation", "explanation", StringComparison.Ordinal)
            .Replace("Points", "points", StringComparison.Ordinal)
            .Replace("SortOrder", "sortOrder", StringComparison.Ordinal)
            .Replace("CorrectBoolean", "correctBoolean", StringComparison.Ordinal)
            .Replace("FillBlankCaseSensitive", "fillBlankCaseSensitive", StringComparison.Ordinal)
            .Replace("AudioResourceId", "audioFileName", StringComparison.Ordinal)
            .Replace("Title", "title", StringComparison.Ordinal)
            .Replace("Description", "description", StringComparison.Ordinal)
            .Replace("Instructions", "instructions", StringComparison.Ordinal)
            .Replace("PassingScorePercentage", "passingScorePercentage", StringComparison.Ordinal);

    private static PaperBatchValidationErrorResponse Issue(int? paperIndex, int? questionIndex, string field, ErrorCodes code)
        => new(paperIndex, questionIndex, field, code, code.GetMessage());

    private static int? QuestionIndex(string propertyName)
    {
        var marker = "Questions[";
        var start = propertyName.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (start < 0) return null;
        start += marker.Length;
        var end = propertyName.IndexOf(']', start);
        return end > start && int.TryParse(propertyName[start..end], out var value) ? value : null;
    }

    private static string NormalizeName(string value) => value.Trim();
    private static string NormalizeAudioName(string value) => AudioResource.NormalizeName(value);

    private sealed record PreparedItem(int PaperIndex, CreatePaperRequest Request);
    private sealed record BuildResult(PaperBatchValidationResponse Response, IReadOnlyList<PreparedItem> Items);
}
