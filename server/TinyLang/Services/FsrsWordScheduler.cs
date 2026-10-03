using System.Text.Json;
using FSRS.Core.Enums;
using FSRS.Core.Models;
using FSRS.Core.Services;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;

namespace TinyLang.Services;

/// <summary>Adapts pinned FSRS-6 scheduling to TinyLang's persistent word progress.</summary>
public static class FsrsWordScheduler
{
    public const string Version = "FSRS-6/default-21/v1/no-fuzz/steps-1m-10m";
    public static readonly WordMemorizationResult[] Ratings =
        [WordMemorizationResult.Again, WordMemorizationResult.Hard, WordMemorizationResult.Good, WordMemorizationResult.Easy];

    public static WordMemorizationResult Normalize(WordMemorizationResult rating) => rating switch
    {
        WordMemorizationResult.Remembered => WordMemorizationResult.Good,
        WordMemorizationResult.Forgotten => WordMemorizationResult.Again,
        _ => rating
    };

    public static Card ReadCard(UserWordProgress? progress, DateTimeOffset now)
    {
        if (progress?.FsrsState is null)
        {
            // Do not fabricate review history or inferred stability for legacy cards.
            return new Card(due: now.UtcDateTime);
        }
        return new Card(state: Enum.Parse<State>(progress.FsrsState), step: progress.LearningStep,
            stability: progress.Stability, difficulty: progress.Difficulty,
            due: (progress.NextReviewAt ?? now).UtcDateTime, lastReview: progress.LastRatedAt?.UtcDateTime);
    }

    public static Card Rate(UserWordProgress? progress, WordMemorizationResult rating,
        DateTimeOffset now, double retention)
    {
        var normalized = Normalize(rating);
        if (!Ratings.Contains(normalized)) throw new ArgumentOutOfRangeException(nameof(rating));
        if (retention is < 0.7 or > 0.97 || !double.IsFinite(retention))
            throw new ArgumentOutOfRangeException(nameof(retention));
        var scheduler = new Scheduler(desiredRetention: retention, enableFuzzing: false);
        return scheduler.ReviewCard(ReadCard(progress, now), normalized switch
        {
            WordMemorizationResult.Again => Rating.Again,
            WordMemorizationResult.Hard => Rating.Hard,
            WordMemorizationResult.Good => Rating.Good,
            _ => Rating.Easy
        }, now.UtcDateTime).UpdatedCard;
    }

    public static IReadOnlyList<WordRatingPreviewResponse> Preview(UserWordProgress? progress,
        DateTimeOffset now, double retention) => Ratings.Select(rating =>
        {
            var card = Rate(progress, rating, now, retention);
            return new WordRatingPreviewResponse(rating, new DateTimeOffset(card.Due),
                (long)(card.Due - now.UtcDateTime).TotalSeconds);
        }).ToArray();

    public static void Apply(UserWordProgress progress, Card card, DateTimeOffset now)
    {
        progress.FsrsState = card.State.ToString();
        progress.Stability = card.Stability;
        progress.Difficulty = card.Difficulty;
        progress.LearningStep = card.Step;
        progress.LastRatedAt = now;
        progress.NextReviewAt = progress.IsReviewExcluded ? null : new DateTimeOffset(card.Due);
        progress.LastStudiedAt = now;
        progress.SchedulerVersion = Version;
        progress.ConcurrencyStamp = Guid.NewGuid();
    }

    public static string Snapshot(UserWordProgress? progress) => JsonSerializer.Serialize(new
    {
        progress?.FsrsState, progress?.Stability, progress?.Difficulty, progress?.LearningStep,
        progress?.LastRatedAt, progress?.NextReviewAt, progress?.ReviewStage
    });
}
