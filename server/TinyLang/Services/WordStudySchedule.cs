using System.Text;

namespace TinyLang.Services;

/// <summary>
/// 封装单词复习间隔和拼写判定的纯业务规则。
/// </summary>
public static class WordStudySchedule
{
    private static readonly int[] SuccessfulIntervals = [2, 4, 7, 15, 30];

    public static (int Stage, DateTimeOffset NextReviewAt) AfterInitialLearning(
        DateTimeOffset completedAt)
        => (0, completedAt.AddDays(1));

    public static (int Stage, DateTimeOffset NextReviewAt) AfterReview(
        int currentStage,
        bool hadFailure,
        DateTimeOffset completedAt)
    {
        ValidateStage(currentStage);

        if (hadFailure)
        {
            return (0, completedAt.AddDays(1));
        }

        var nextStage = Math.Min(currentStage + 1, 5);
        var days = currentStage >= 5
            ? 30
            : SuccessfulIntervals[currentStage];
        return (nextStage, completedAt.AddDays(days));
    }

    public static bool IsCorrectSpelling(string answer, string headword)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ArgumentNullException.ThrowIfNull(headword);

        return string.Equals(
            NormalizeSpelling(answer),
            NormalizeSpelling(headword),
            StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeSpelling(string value)
        => value.Trim().Normalize(NormalizationForm.FormC);

    private static void ValidateStage(int reviewStage)
    {
        if (reviewStage is < 0 or > 5)
        {
            throw new ArgumentOutOfRangeException(nameof(reviewStage));
        }
    }
}
