using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>Append-only FSRS rating event, committed with card and session state.</summary>
public sealed class WordReviewLog : BaseEntity
{
    public WordReviewLog() { Id = Guid.NewGuid(); }
    public Guid UserId { get; set; }
    public Guid WordId { get; set; }
    public Guid SessionItemId { get; set; }
    public Guid SubmissionStamp { get; set; }
    public WordMemorizationResult Rating { get; set; }
    public DateTimeOffset RatedAt { get; set; }
    public DateTimeOffset DueAt { get; set; }
    public double DesiredRetention { get; set; }
    public string SchedulerVersion { get; set; } = "";
    public bool ImportedFromLegacy { get; set; }
    public string BeforeStateJson { get; set; } = "";
    public string AfterStateJson { get; set; } = "";
}
