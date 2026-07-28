using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证视频元数据、字幕语言和播放位置 DTO 边界。
/// </summary>
public sealed class VideoValidatorsTests
{
    [Fact]
    public async Task CreateVideoShouldRejectInvalidSourceAndLanguage()
    {
        var validator = new CreateVideoRequestValidator();

        var result = await validator.ValidateAsync(new CreateVideoRequest
        {
            SourceMediaResourceId = Guid.Empty,
            Title = "video",
            OriginalLanguage = "not_a_language"
        }, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Select(value => value.PropertyName).Should().Contain([
            nameof(CreateVideoRequest.SourceMediaResourceId),
            nameof(CreateVideoRequest.OriginalLanguage)
        ]);
    }

    [Fact]
    public async Task CreateVideoShouldRejectDuplicateAndTooManyCategories()
    {
        var categoryId = Guid.NewGuid();
        var validator = new CreateVideoRequestValidator();

        var duplicate = await validator.ValidateAsync(
            new CreateVideoRequest
            {
                SourceMediaResourceId = Guid.NewGuid(),
                Title = "Video",
                OriginalLanguage = "en",
                CategoryIds = [categoryId, categoryId]
            },
            TestContext.Current.CancellationToken);
        var tooMany = await validator.ValidateAsync(
            new CreateVideoRequest
            {
                SourceMediaResourceId = Guid.NewGuid(),
                Title = "Video",
                OriginalLanguage = "en",
                CategoryIds = Enumerable.Range(0, 11)
                    .Select(_ => Guid.NewGuid())
                    .ToArray()
            },
            TestContext.Current.CancellationToken);

        duplicate.Errors.Should().Contain(error =>
            error.PropertyName == nameof(CreateVideoRequest.CategoryIds));
        tooMany.Errors.Should().Contain(error =>
            error.PropertyName == nameof(CreateVideoRequest.CategoryIds));
    }

    [Theory]
    [InlineData("double--dash")]
    [InlineData("-leading")]
    [InlineData("trailing-")]
    public async Task VideoCategoryShouldRejectInvalidSlug(string slug)
    {
        var validator = new CreateVideoCategoryRequestValidator();

        var result = await validator.ValidateAsync(
            new CreateVideoCategoryRequest { Name = "Category", Slug = slug },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task VideoCategoryListShouldRejectEmptyCategoryFilter()
    {
        var validator = new EditorVideoListRequestValidator();

        var result = await validator.ValidateAsync(
            new EditorVideoListRequest { CategoryId = Guid.Empty },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Fact]
    public async Task AdminVideoCategoryListShouldRejectInvalidPageSize()
    {
        var validator = new AdminVideoCategoryListRequestValidator();

        var result = await validator.ValidateAsync(
            new AdminVideoCategoryListRequest { PageSize = 101 },
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("en")]
    [InlineData("zh-Hans")]
    [InlineData("en-US")]
    public async Task SubtitleShouldAcceptBcp47StyleLanguage(string language)
    {
        var validator = new AddVideoSubtitleRequestValidator();

        var result = await validator.ValidateAsync(new AddVideoSubtitleRequest
        {
            MediaResourceId = Guid.NewGuid(),
            LanguageTag = language,
            DisplayName = "Subtitle"
        }, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public async Task ProgressShouldRejectNonFiniteOrNegativePosition(double position)
    {
        var validator = new UpdateVideoProgressRequestValidator();

        var result = await validator.ValidateAsync(new UpdateVideoProgressRequest
        {
            PositionSeconds = position
        }, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
    }
}
