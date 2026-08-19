using TinyLang.Entities.Enums;
using TinyLang.Exceptions;

namespace TinyLang.Dtos;

/// <summary>
/// 描述一次试卷批量校验或导入请求。批次采用整批原子语义。
/// </summary>
public sealed record PaperBatchRequest
{
    public IReadOnlyCollection<PaperBatchItemRequest> Papers { get; init; } = [];
}

/// <summary>
/// 描述批量 JSON 中的一张试卷。导入时服务端强制保存为草稿。
/// </summary>
public sealed record PaperBatchItemRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public IReadOnlyCollection<string> CategoryNames { get; init; } = [];
    public int PassingScorePercentage { get; init; } = 60;
    public IReadOnlyCollection<PaperBatchQuestionRequest> Questions { get; init; } = [];
}

/// <summary>
/// 描述批量 JSON 中的一道题。仅 Dictation 使用音频文件名和 blanks。
/// </summary>
public sealed record PaperBatchQuestionRequest
{
    public PaperQuestionType Type { get; init; }
    public required string Prompt { get; init; }
    public string? Explanation { get; init; }
    public int Points { get; init; }
    public int SortOrder { get; init; }
    public bool? CorrectBoolean { get; init; }
    public bool FillBlankCaseSensitive { get; init; }
    public IReadOnlyCollection<PaperQuestionOptionInput> Options { get; init; } = [];
    public IReadOnlyCollection<FillBlankAcceptedAnswerInput> AcceptedAnswers { get; init; } = [];
    public string? AudioFileName { get; init; }
    public IReadOnlyCollection<PaperDictationBlankInput> Blanks { get; init; } = [];
}

public sealed record PaperBatchSummaryResponse(
    int PaperCount,
    int QuestionCount,
    int DictationQuestionCount,
    int DictationBlankCount,
    int CategoryReferenceCount,
    int AudioReferenceCount);

public sealed record PaperBatchValidationErrorResponse(
    int? PaperIndex,
    int? QuestionIndex,
    string Field,
    ErrorCodes ErrorCode,
    string Message);

public sealed record PaperBatchPaperValidationResponse(
    int PaperIndex,
    string? Title,
    bool IsValid,
    int QuestionCount,
    IReadOnlyList<string> CategoryNames,
    IReadOnlyList<string> AudioFileNames);

public sealed record PaperBatchValidationResponse(
    bool IsValid,
    PaperBatchSummaryResponse Summary,
    IReadOnlyList<PaperBatchPaperValidationResponse> Papers,
    IReadOnlyList<PaperBatchValidationErrorResponse> Errors);

public sealed record PaperBatchCreatedItemResponse(int PaperIndex, Guid PaperId);

public sealed record PaperBatchImportResponse(
    int ImportedCount,
    IReadOnlyList<PaperBatchCreatedItemResponse> Items);

public sealed record PaperBatchImportResult(
    PaperBatchImportResponse? Imported,
    PaperBatchValidationResponse? Validation);

