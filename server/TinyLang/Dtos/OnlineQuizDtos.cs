using System.ComponentModel.DataAnnotations;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;

namespace TinyLang.Dtos;

/// <summary>
/// 定义创建和更新试卷共享的完整目标内容。
/// </summary>
public abstract record PaperUpsertRequest
{
    public required string Title { get; init; }
    public string? Description { get; init; }
    public string? Instructions { get; init; }
    public required string LanguageTag { get; init; }
    public IReadOnlyCollection<string> Tags { get; init; } = [];
    public int PassingScore { get; init; }
    public IReadOnlyCollection<PaperQuestionInput> Questions { get; init; } = [];
}

/// <summary>
/// 描述创建试卷草稿及其当前题目集合的请求。
/// </summary>
public sealed record CreatePaperRequest : PaperUpsertRequest;

/// <summary>
/// 描述以完整目标集合和乐观并发标识更新试卷的请求。
/// </summary>
public sealed record UpdatePaperRequest : PaperUpsertRequest
{
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// 描述发布检查、状态动作和硬删除使用的并发前置条件。
/// </summary>
public sealed record PaperMutationRequest
{
    [Required]
    public Guid ConcurrencyStamp { get; init; }
}

/// <summary>
/// 描述一道待新增或更新的试卷题目及其完整答案集合。
/// </summary>
public sealed record PaperQuestionInput
{
    public Guid? Id { get; init; }
    public PaperQuestionType Type { get; init; }
    public required string Prompt { get; init; }
    public string? Explanation { get; init; }
    public int Points { get; init; }
    public int SortOrder { get; init; }
    public bool? CorrectBoolean { get; init; }
    public bool FillBlankCaseSensitive { get; init; }
    public IReadOnlyCollection<PaperQuestionOptionInput> Options { get; init; } = [];
    public IReadOnlyCollection<FillBlankAcceptedAnswerInput> AcceptedAnswers { get; init; } = [];
}

/// <summary>
/// 描述单选题中一个待新增或更新的选项。
/// </summary>
public sealed record PaperQuestionOptionInput
{
    public Guid? Id { get; init; }
    public required string Text { get; init; }
    public bool IsCorrect { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 描述填空题中一种待新增或更新的可接受答案。
/// </summary>
public sealed record FillBlankAcceptedAnswerInput
{
    public Guid? Id { get; init; }
    public required string Text { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>
/// 描述管理员试卷列表的筛选和分页条件。
/// </summary>
public sealed record AdminPaperListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public string? Language { get; init; }
    public string? Tag { get; init; }
    public PaperPublicationStatus? Status { get; init; }
}

/// <summary>
/// 描述登录用户试卷目录的筛选和分页条件。
/// </summary>
public sealed record PaperCatalogRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
    public string? Language { get; init; }
    public string? Tag { get; init; }
}

/// <summary>
/// 描述试卷标签目录的分页和名称搜索条件。
/// </summary>
public sealed record PaperTagListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    public string? Keyword { get; init; }
}

/// <summary>
/// 返回规范标签及其可见试卷数量。
/// </summary>
public sealed record PaperTagSummaryResponse(string Name, int PaperCount);
/// <summary>
/// 描述当前用户指定试卷测验历史的分页条件。
/// </summary>
public sealed record PaperAttemptListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

/// <summary>
/// 描述用户为一道题保存的单一题型答案。
/// </summary>
public sealed record SavePaperAttemptAnswerRequest
{
    public Guid? SelectedOptionId { get; init; }
    public bool? BooleanAnswer { get; init; }
    public string? TextAnswer { get; init; }
}

/// <summary>
/// 返回管理员管理单选题选项所需的标准答案标记。
/// </summary>
public sealed record AdminPaperQuestionOptionResponse(
    Guid Id,
    string Text,
    bool IsCorrect,
    int SortOrder);

/// <summary>
/// 返回管理员管理填空题标准答案所需的展示内容。
/// </summary>
public sealed record AdminFillBlankAcceptedAnswerResponse(
    Guid Id,
    string Text,
    int SortOrder);

/// <summary>
/// 返回管理员管理一道题目所需的完整标准答案和解析。
/// </summary>
public sealed record AdminPaperQuestionResponse(
    Guid Id,
    PaperQuestionType Type,
    string Prompt,
    string? Explanation,
    int Points,
    int SortOrder,
    bool? CorrectBoolean,
    bool FillBlankCaseSensitive,
    IReadOnlyList<AdminPaperQuestionOptionResponse> Options,
    IReadOnlyList<AdminFillBlankAcceptedAnswerResponse> AcceptedAnswers);

/// <summary>
/// 返回一个可映射到管理表单的稳定发布检查问题。
/// </summary>
public sealed record PaperValidationIssueResponse(
    string Field,
    ErrorCodes ErrorCode,
    string Message,
    Guid? QuestionId,
    Guid? ChildId);

/// <summary>
/// 返回不修改试卷的发布前检查结果。
/// </summary>
public sealed record PaperValidationResponse(
    bool IsValid,
    IReadOnlyList<PaperValidationIssueResponse> Issues);

/// <summary>
/// 返回试卷完整编辑状态、审计信息、并发标识和标准答案。
/// </summary>
public sealed record AdminPaperResponse(
    Guid Id,
    string Title,
    string? Description,
    string? Instructions,
    string LanguageTag,
    IReadOnlyList<string> Tags,
    PaperPublicationStatus Status,
    int PassingScore,
    int TotalScore,
    int AttemptCount,
    ContentAuditUserResponse CreatedBy,
    ContentAuditUserResponse LastEditor,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    Guid ConcurrencyStamp,
    IReadOnlyList<AdminPaperQuestionResponse> Questions,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

/// <summary>
/// 返回管理员试卷列表中的轻量管理摘要。
/// </summary>
public sealed record AdminPaperListItemResponse(
    Guid Id,
    string Title,
    string LanguageTag,
    IReadOnlyList<string> Tags,
    PaperPublicationStatus Status,
    int QuestionCount,
    int TotalScore,
    int PassingScore,
    int AttemptCount,
    ContentAuditUserResponse CreatedBy,
    ContentAuditUserResponse LastEditor,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid ConcurrencyStamp);

/// <summary>
/// 返回用户试卷目录中的安全发布摘要。
/// </summary>
public sealed record PaperCatalogItemResponse(
    Guid Id,
    string Title,
    string? Description,
    string LanguageTag,
    IReadOnlyList<string> Tags,
    int QuestionCount,
    int TotalScore,
    int PassingScore,
    DateTimeOffset PublishedAt);

/// <summary>
/// 返回用户可见的试卷说明和评分阈值，不包含题目答案。
/// </summary>
public sealed record PaperDetailsResponse(
    Guid Id,
    string Title,
    string? Description,
    string? Instructions,
    string LanguageTag,
    IReadOnlyList<string> Tags,
    int QuestionCount,
    int TotalScore,
    int PassingScore,
    DateTimeOffset PublishedAt);

/// <summary>
/// 返回用户作答时可见的单选题选项，不包含正确标记。
/// </summary>
public sealed record UserPaperAttemptOptionResponse(
    Guid Id,
    string Text,
    int SortOrder);

/// <summary>
/// 返回用户当前为一道题保存的答案，不包含判分信息。
/// </summary>
public sealed record UserPaperAttemptSavedAnswerResponse(
    Guid? SelectedOptionId,
    bool? BooleanAnswer,
    string? TextAnswer,
    DateTimeOffset? SavedAt);

/// <summary>
/// 返回用户作答所需的题干、选项和已保存答案。
/// </summary>
public sealed record UserPaperAttemptQuestionResponse(
    Guid Id,
    PaperQuestionType Type,
    string Prompt,
    int Points,
    int SortOrder,
    IReadOnlyList<UserPaperAttemptOptionResponse> Options,
    UserPaperAttemptSavedAnswerResponse? SavedAnswer);

/// <summary>
/// 返回可恢复的测验和安全试卷内容，不包含答案、解析或成绩。
/// </summary>
public sealed record UserPaperAttemptResponse(
    Guid Id,
    Guid PaperId,
    int AttemptNumber,
    PaperAttemptStatus Status,
    string Title,
    string? Description,
    string? Instructions,
    string LanguageTag,
    int QuestionCount,
    int PaperTotalScore,
    int PaperPassingScore,
    DateTimeOffset StartedAt,
    DateTimeOffset? SubmittedAt,
    IReadOnlyList<UserPaperAttemptQuestionResponse> Questions);

/// <summary>
/// 返回当前用户一次测验的生命周期和可空提交成绩摘要。
/// </summary>
public sealed record PaperAttemptSummaryResponse(
    Guid Id,
    Guid PaperId,
    int AttemptNumber,
    PaperAttemptStatus Status,
    int? Score,
    int PaperTotalScore,
    bool? IsPassed,
    DateTimeOffset StartedAt,
    DateTimeOffset? SubmittedAt);

/// <summary>
/// 返回提交结果中的单选题选项文本。
/// </summary>
public sealed record PaperAttemptResultOptionResponse(
    Guid Id,
    string Text,
    int SortOrder);

/// <summary>
/// 返回一道已提交题目的用户答案、标准答案、解析和得分。
/// </summary>
public sealed record PaperAttemptQuestionResultResponse(
    Guid QuestionId,
    PaperQuestionType Type,
    string Prompt,
    string? Explanation,
    int Points,
    int SortOrder,
    IReadOnlyList<PaperAttemptResultOptionResponse> Options,
    Guid? SelectedOptionId,
    bool? BooleanAnswer,
    string? TextAnswer,
    bool IsAnswered,
    Guid? CorrectOptionId,
    bool? CorrectBoolean,
    IReadOnlyList<string> AcceptedAnswers,
    bool IsCorrect,
    int AwardedPoints);

/// <summary>
/// 返回已提交测验的稳定总成绩、通过状态和完整逐题结果。
/// </summary>
public sealed record PaperAttemptResultResponse(
    Guid Id,
    Guid PaperId,
    int AttemptNumber,
    string PaperTitle,
    int Score,
    int PaperTotalScore,
    int PaperPassingScore,
    bool IsPassed,
    DateTimeOffset StartedAt,
    DateTimeOffset SubmittedAt,
    IReadOnlyList<PaperAttemptQuestionResultResponse> Questions);

/// <summary>
/// 表示开始测验用例创建新记录还是恢复既有活动记录。
/// </summary>
public readonly record struct PaperAttemptStartOutcome(
    UserPaperAttemptResponse Attempt,
    bool WasCreated);

/// <summary>
/// 定义在线试卷请求和持久化模型共享的有界限制。
/// </summary>
public static class OnlineQuizConstraints
{
    public const int MaxTitleLength = 200;
    public const int MaxDescriptionLength = 2000;
    public const int MaxInstructionsLength = 5000;
    public const int MaxPromptLength = 5000;
    public const int MaxExplanationLength = 5000;
    public const int MaxOptionTextLength = 2000;
    public const int MaxAnswerTextLength = 1000;
    public const int MaxLanguageTagLength = 35;
    public const int MaxPaperTagCount = 10;
    public const int MaxPaperTagLength = 30;
    public const int MaxQuestionCount = 200;
    public const int MaxOptionCount = 10;
    public const int MaxAcceptedAnswerCount = 20;
    public const int MinPoints = 1;
    public const int MaxPoints = 100;
    public const int MaxSortOrder = 10_000;
    public const int MaxTotalScore = MaxQuestionCount * MaxPoints;
}
