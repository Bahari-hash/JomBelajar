using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证音频创建、更新和列表请求的字段及枚举边界。
/// </summary>
public sealed class AudioValidatorsTests
{
    [Fact]
    public async Task RenameShouldRejectBlankAndControlCharacters()
    {
        var validator = new RenameAudioResourceRequestValidator();

        (await validator.ValidateAsync(
            new RenameAudioResourceRequest { Name = " " },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(
            new RenameAudioResourceRequest { Name = "lesson\n.mp3" },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(
            new RenameAudioResourceRequest { Name = "lesson (2).mp3" },
            TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
    }

    [Fact]
    public async Task AdminListShouldValidateAudioResourceStatus()
    {
        var validator = new AdminAudioResourceListRequestValidator();

        (await validator.ValidateAsync(
            new AdminAudioResourceListRequest
            {
                Status = (AudioResourceStatus)999
            },
            TestContext.Current.CancellationToken)).IsValid.Should().BeFalse();
        (await validator.ValidateAsync(
            new AdminAudioResourceListRequest
            {
                Status = AudioResourceStatus.Ready,
                Keyword = "lesson.mp3"
            },
            TestContext.Current.CancellationToken)).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证合法音频元数据和 BCP 47 风格语言标签可通过。
    /// </summary>
    [Fact]
    public async Task CreateAudioShouldAcceptValidMetadata()
    {
        var validator = new CreateAudioClipRequestValidator();

        var result = await validator.ValidateAsync(new CreateAudioClipRequest
        {
            SourceMediaResourceId = Guid.NewGuid(),
            Title = "Pronunciation",
            Description = "Native speaker",
            Kind = AudioClipKind.WordPronunciation
        }, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证空 source、标题和非法语言、用途按对应字段失败。
    /// </summary>
    [Fact]
    public async Task CreateAudioShouldRejectInvalidRequiredMetadata()
    {
        var validator = new CreateAudioClipRequestValidator();

        var result = await validator.ValidateAsync(new CreateAudioClipRequest
        {
            SourceMediaResourceId = Guid.Empty,
            Title = string.Empty,
            Kind = (AudioClipKind)999
        }, TestContext.Current.CancellationToken);

        result.Errors.Select(value => value.PropertyName).Should().Contain([
            nameof(CreateAudioClipRequest.SourceMediaResourceId),
            nameof(CreateAudioClipRequest.Title),
            nameof(CreateAudioClipRequest.Kind)
        ]);
    }

    /// <summary>
    /// 验证更新请求的标题和简介最大长度限制。
    /// </summary>
    [Fact]
    public async Task UpdateAudioShouldRejectOversizedText()
    {
        var validator = new UpdateAudioClipRequestValidator();

        var result = await validator.ValidateAsync(new UpdateAudioClipRequest
        {
            Title = new string('t', 201),
            Description = new string('d', 2001),
            Kind = AudioClipKind.Other
        }, TestContext.Current.CancellationToken);

        result.Errors.Select(value => value.PropertyName).Should().Contain([
            nameof(UpdateAudioClipRequest.Title),
            nameof(UpdateAudioClipRequest.Description)
        ]);
    }

    /// <summary>
    /// 验证分页和可空枚举筛选仍拒绝越界值。
    /// </summary>
    [Fact]
    public async Task AdminAudioListShouldRejectInvalidPagingAndEnums()
    {
        var validator = new AdminAudioClipListRequestValidator();

        var result = await validator.ValidateAsync(new AdminAudioClipListRequest
        {
            Page = 0,
            PageSize = 101,
            ProcessingStatus = (AudioProcessingStatus)999,
            PublicationStatus = (AudioPublicationStatus)999,
            Kind = (AudioClipKind)999
        }, TestContext.Current.CancellationToken);

        result.IsValid.Should().BeFalse();
        result.Errors.Should().HaveCount(5);
    }

    /// <summary>
    /// 验证管理员音频列表的默认筛选请求有效。
    /// </summary>
    [Fact]
    public async Task AdminAudioListShouldAcceptDefaultFilters()
    {
        var validator = new AdminAudioClipListRequestValidator();

        var result = await validator.ValidateAsync(
            new AdminAudioClipListRequest(),
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
    }
}
