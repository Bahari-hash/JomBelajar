using System.IO;
using System.Text.Json;
using FSRS.Core.Enums;
using FSRS.Core.Models;
using FSRS.Core.Services;
using FluentAssertions;

namespace TinyLang.UnitTests;

public sealed class FsrsReferenceTests
{
    [Fact]
    public void SchedulerMatchesOfficialPythonReferenceAcrossStatesAndRatings()
    {
        var rows = JsonDocument.Parse(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures/fsrs-reference.json")));
        var now = new DateTime(2026, 10, 3, 0, 0, 0, DateTimeKind.Utc);
        foreach (var r in rows.RootElement.EnumerateArray())
        {
            double? Number(string key) => r.GetProperty(key).ValueKind == JsonValueKind.Null ? null : r.GetProperty(key).GetDouble();
            var c = new Card(state: Enum.Parse<State>(r.GetProperty("state").GetString()!),
                step: (int?)Number("step"), stability: Number("stability"), difficulty: Number("difficulty"),
                due: now, lastReview: Number("stability") is null ? null : now.AddDays(-r.GetProperty("elapsed").GetDouble()));
            var result = new Scheduler(enableFuzzing: false).ReviewCard(c,
                Enum.Parse<Rating>(r.GetProperty("rating").GetString()!), now).UpdatedCard;
            result.State.ToString().Should().Be(r.GetProperty("nextState").GetString());
            result.Step.Should().Be((int?)Number("nextStep"));
            result.Stability!.Value.Should().BeApproximately(Number("nextStability")!.Value, 1e-8);
            result.Difficulty!.Value.Should().BeApproximately(Number("nextDifficulty")!.Value, 1e-8);
            (result.Due - now).TotalSeconds.Should().Be(Number("seconds"));
        }
    }
}
