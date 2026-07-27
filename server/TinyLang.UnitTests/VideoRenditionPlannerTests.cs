using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Options;
using TinyLang.Services;
using TinyLang.Settings;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证三档 HLS 规划和不放大边界。
/// </summary>
public sealed class VideoRenditionPlannerTests
{
    [Fact]
    public void FullHdSourceShouldProduce480p720pAnd1080p()
    {
        var planner = CreatePlanner();

        var result = planner.CreatePlan(1980, 1080);

        result.Select(value => value.TargetHeight).Should().Equal(480, 720, 1080);
        result.Select(value => (value.Width, value.Height)).Should().Equal(
            (880, 480),
            (1320, 720),
            (1980, 1080));
        result.Select(value => value.VideoBitrateKbps).Should().Equal(1200, 2500, 4500);
    }

    [Fact]
    public void SevenTwentySourceShouldSkip1080pWithoutUpscaling()
    {
        var planner = CreatePlanner();

        var result = planner.CreatePlan(1280, 720);

        result.Select(value => value.TargetHeight).Should().Equal(480, 720);
        result[0].Width.Should().Be(854);
        result.Should().OnlyContain(value => value.Width <= 1280 && value.Height <= 720);
    }

    [Fact]
    public void SmallSourceShouldProduceSingleEvenSourceSizedRendition()
    {
        var planner = CreatePlanner();

        var result = planner.CreatePlan(641, 359);

        result.Should().ContainSingle();
        result[0].TargetHeight.Should().Be(358);
        result[0].Width.Should().BeLessThanOrEqualTo(640);
        (result[0].Width % 2).Should().Be(0);
        result[0].Height.Should().Be(358);
    }

    private static VideoRenditionPlanner CreatePlanner()
        => new(Options.Create(new VideoProcessingSettings()));
}
