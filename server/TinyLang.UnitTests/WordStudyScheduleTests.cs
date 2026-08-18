using FluentAssertions;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class WordStudyScheduleTests
{
    private static readonly DateTimeOffset CompletedAt =
        new(2026, 8, 18, 8, 30, 0, TimeSpan.Zero);

    [Fact]
    public void AfterInitialLearningShouldScheduleStageZeroAfterOneDay()
    {
        var schedule = WordStudySchedule.AfterInitialLearning(CompletedAt);

        schedule.Stage.Should().Be(0);
        schedule.NextReviewAt.Should().Be(CompletedAt.AddDays(1));
    }

    [Theory]
    [InlineData(0, 1, 2)]
    [InlineData(1, 2, 4)]
    [InlineData(2, 3, 7)]
    [InlineData(3, 4, 15)]
    [InlineData(4, 5, 30)]
    [InlineData(5, 5, 30)]
    public void SuccessfulReviewShouldAdvanceAccordingToSchedule(
        int currentStage,
        int expectedStage,
        int expectedDays)
    {
        var schedule = WordStudySchedule.AfterReview(
            currentStage,
            hadFailure: false,
            CompletedAt);

        schedule.Stage.Should().Be(expectedStage);
        schedule.NextReviewAt.Should().Be(CompletedAt.AddDays(expectedDays));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void FailedReviewShouldResetToStageZeroAfterOneDay(int currentStage)
    {
        var schedule = WordStudySchedule.AfterReview(
            currentStage,
            hadFailure: true,
            CompletedAt);

        schedule.Stage.Should().Be(0);
        schedule.NextReviewAt.Should().Be(CompletedAt.AddDays(1));
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(6)]
    public void ReviewShouldRejectInvalidStage(int currentStage)
    {
        var act = () => WordStudySchedule.AfterReview(
            currentStage,
            hadFailure: false,
            CompletedAt);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Theory]
    [InlineData(" caf\u00e9 ", "CAFE\u0301")]
    [InlineData("mother-in-law", " MOTHER-IN-LAW ")]
    [InlineData("l'amour", "L'AMOUR")]
    public void SpellingShouldIgnoreCaseWhitespaceAndUnicodeComposition(
        string expected,
        string actual)
    {
        WordStudySchedule.IsCorrectSpelling(actual, expected).Should().BeTrue();
    }

    [Theory]
    [InlineData("cafe", "caf\u00e9")]
    [InlineData("mother-in-law", "mother in law")]
    [InlineData("l'amour", "lamour")]
    public void SpellingShouldPreserveAccentsHyphensAndApostrophes(
        string expected,
        string actual)
    {
        WordStudySchedule.IsCorrectSpelling(actual, expected).Should().BeFalse();
    }

    [Fact]
    public void SpellingShouldRejectNullAnswer()
    {
        var act = () => WordStudySchedule.IsCorrectSpelling(null!, "word");

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("answer");
    }

    [Fact]
    public void SpellingShouldRejectNullHeadword()
    {
        var act = () => WordStudySchedule.IsCorrectSpelling("word", null!);

        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("headword");
    }
}
