using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;

namespace TinyLang.Services;

/// <summary>
/// 统一校验、规范化和判定测验保存答案与错题重做答案。
/// </summary>
internal static class PaperAnswerEvaluator
{
    public static PreparedPaperAnswer Prepare(
        PaperQuestion question,
        SavePaperAttemptAnswerRequest request)
        => question.Type switch
        {
            PaperQuestionType.SingleChoice => PrepareSingleChoice(question, request),
            PaperQuestionType.TrueFalse => PrepareTrueFalse(request),
            PaperQuestionType.FillBlank => PrepareFillBlank(question, request),
            PaperQuestionType.Dictation => PrepareDictation(question, request),
            _ => throw InvalidShape()
        };

    public static void Apply(
        PaperAttemptAnswer answer,
        PreparedPaperAnswer prepared,
        DateTimeOffset savedAt)
    {
        answer.SelectedOptionId = prepared.SelectedOptionId;
        answer.BooleanAnswer = prepared.BooleanAnswer;
        answer.TextAnswer = prepared.TextAnswer;
        answer.NormalizedTextAnswer = prepared.NormalizedTextAnswer;
        answer.TextAnswers = prepared.TextAnswers?.ToArray();
        answer.IsAnswered = true;
        answer.IsCorrect = null;
        answer.AwardedPoints = null;
        answer.SavedAt = savedAt;
        answer.ConcurrencyStamp = Guid.NewGuid();
    }

    public static bool Matches(
        PaperAttemptAnswer answer,
        PreparedPaperAnswer prepared)
        => answer.IsAnswered &&
            answer.SelectedOptionId == prepared.SelectedOptionId &&
            answer.BooleanAnswer == prepared.BooleanAnswer &&
            string.Equals(answer.TextAnswer, prepared.TextAnswer, StringComparison.Ordinal) &&
            string.Equals(
                answer.NormalizedTextAnswer,
                prepared.NormalizedTextAnswer,
                StringComparison.Ordinal) &&
            SequenceEqual(answer.TextAnswers, prepared.TextAnswers);

    public static bool Score(PaperQuestion question, PaperAttemptAnswer answer)
    {
        if (!answer.IsAnswered)
        {
            return false;
        }

        return question.Type switch
        {
            PaperQuestionType.SingleChoice => question.Options.Any(value =>
                value.IsCorrect && answer.SelectedOptionId == value.Id),
            PaperQuestionType.TrueFalse =>
                answer.BooleanAnswer == question.CorrectBoolean,
            PaperQuestionType.FillBlank =>
                answer.NormalizedTextAnswer is { } normalized &&
                question.AcceptedAnswers.Any(value => value.NormalizedText == normalized),
            PaperQuestionType.Dictation => ScoreDictation(question, answer.TextAnswers),
            _ => false
        };
    }

    public static bool Score(
        PaperQuestion question,
        PreparedPaperAnswer prepared)
        => question.Type switch
        {
            PaperQuestionType.SingleChoice => question.Options.Any(value =>
                value.IsCorrect && prepared.SelectedOptionId == value.Id),
            PaperQuestionType.TrueFalse =>
                prepared.BooleanAnswer == question.CorrectBoolean,
            PaperQuestionType.FillBlank =>
                prepared.NormalizedTextAnswer is { } normalized &&
                question.AcceptedAnswers.Any(value => value.NormalizedText == normalized),
            PaperQuestionType.Dictation => ScoreDictation(question, prepared.TextAnswers),
            _ => false
        };

    private static PreparedPaperAnswer PrepareSingleChoice(
        PaperQuestion question,
        SavePaperAttemptAnswerRequest request)
    {
        if (request.SelectedOptionId is not { } selectedOptionId ||
            request.BooleanAnswer.HasValue || request.TextAnswer is not null ||
            request.TextAnswers is not null)
        {
            throw InvalidShape();
        }
        if (!question.Options.Any(value => value.Id == selectedOptionId))
        {
            throw new RequestValidationException(
                ErrorCodes.PaperAttemptSelectedOptionInvalid);
        }
        return new PreparedPaperAnswer(selectedOptionId, null, null, null, null);
    }

    private static PreparedPaperAnswer PrepareTrueFalse(
        SavePaperAttemptAnswerRequest request)
    {
        if (!request.BooleanAnswer.HasValue || request.SelectedOptionId.HasValue ||
            request.TextAnswer is not null || request.TextAnswers is not null)
        {
            throw InvalidShape();
        }
        return new PreparedPaperAnswer(null, request.BooleanAnswer, null, null, null);
    }

    private static PreparedPaperAnswer PrepareFillBlank(
        PaperQuestion question,
        SavePaperAttemptAnswerRequest request)
    {
        if (request.TextAnswer is not { } textAnswer ||
            request.SelectedOptionId.HasValue || request.BooleanAnswer.HasValue ||
            request.TextAnswers is not null)
        {
            throw InvalidShape();
        }

        string normalized;
        try
        {
            normalized = FillBlankAnswerNormalizer.Normalize(
                textAnswer,
                question.FillBlankCaseSensitive);
        }
        catch (ArgumentException)
        {
            throw InvalidShape();
        }
        if (normalized.Length is < 1 or > OnlineQuizConstraints.MaxAnswerTextLength)
        {
            throw InvalidShape();
        }
        return new PreparedPaperAnswer(null, null, textAnswer, normalized, null);
    }

    private static PreparedPaperAnswer PrepareDictation(
        PaperQuestion question,
        SavePaperAttemptAnswerRequest request)
    {
        if (request.TextAnswers is not { } textAnswers ||
            request.SelectedOptionId.HasValue || request.BooleanAnswer.HasValue ||
            request.TextAnswer is not null ||
            textAnswers.Count != question.DictationBlanks.Count)
        {
            throw InvalidShape();
        }

        var answers = textAnswers.ToArray();
        if (answers.Any(value => value is null ||
            value.Length > OnlineQuizConstraints.MaxAnswerTextLength))
        {
            throw InvalidShape();
        }
        return new PreparedPaperAnswer(null, null, null, null, answers);
    }

    private static bool ScoreDictation(
        PaperQuestion question,
        IReadOnlyList<string>? textAnswers)
    {
        var blanks = question.DictationBlanks.OrderBy(value => value.SortOrder)
            .ThenBy(value => value.Id)
            .ToArray();
        if (textAnswers is null || textAnswers.Count != blanks.Length)
        {
            return false;
        }

        for (var index = 0; index < blanks.Length; index++)
        {
            if (!string.Equals(
                NormalizeDictation(textAnswers[index]),
                blanks[index].NormalizedAnswer,
                StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    private static string NormalizeDictation(string value)
        => value.Trim().ToUpperInvariant();

    private static bool SequenceEqual(
        IReadOnlyList<string>? first,
        IReadOnlyList<string>? second)
        => first is null
            ? second is null
            : second is not null && first.SequenceEqual(second, StringComparer.Ordinal);

    private static RequestValidationException InvalidShape()
        => new(ErrorCodes.PaperAttemptAnswerShapeInvalid);
}

internal readonly record struct PreparedPaperAnswer(
    Guid? SelectedOptionId,
    bool? BooleanAnswer,
    string? TextAnswer,
    string? NormalizedTextAnswer,
    IReadOnlyList<string>? TextAnswers);
