using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证共享音频资源的创建约定和 EF Core 映射。
/// </summary>
public sealed class AudioResourceModelTests
{
    [Fact]
    public void CreateShouldPreserveFileNameAndStartUploading()
    {
        var adminId = Guid.NewGuid();
        var sourceMediaResourceId = Guid.NewGuid();

        var audio = AudioResource.Create(
            adminId,
            "  Lesson.MP3  ",
            sourceMediaResourceId);

        audio.Id.Should().NotBeEmpty();
        audio.CreatedById.Should().Be(adminId);
        audio.LastEditorId.Should().Be(adminId);
        audio.SourceMediaResourceId.Should().Be(sourceMediaResourceId);
        audio.Name.Should().Be("Lesson.MP3");
        audio.NormalizedName.Should().Be("lesson.mp3");
        audio.Status.Should().Be(AudioResourceStatus.Uploading);
        audio.ConcurrencyStamp.Should().NotBeEmpty();
    }

    [Fact]
    public void ModelShouldUseNormalizedNameAndRestrictSourceDeletion()
    {
        using var db = CreateDbContext();
        var audioResource = db.Model.FindEntityType(typeof(AudioResource));

        audioResource.Should().NotBeNull();
        audioResource!.GetIndexes().Should().Contain(index =>
            index.IsUnique &&
            index.Properties.Select(value => value.Name).SequenceEqual(
                new[] { nameof(AudioResource.NormalizedName) }));
        audioResource.GetForeignKeys().Single(value =>
                value.PrincipalEntityType.ClrType == typeof(MediaResource))
            .DeleteBehavior.Should().Be(DeleteBehavior.Restrict);
    }

    [Fact]
    public void DbContextAndModelShouldNotExposeLegacyAudioClips()
    {
        typeof(ApplicationDbContext).GetProperty("AudioClips").Should().BeNull();
        typeof(IApplicationDbContext).GetProperty("AudioClips").Should().BeNull();

        using var db = CreateDbContext();
        db.Model.FindEntityType("TinyLang.Entities.AudioClip").Should().BeNull();
    }

    [Fact]
    public void AudioProcessingJobShouldRequireOnlyAudioResource()
    {
        typeof(AudioProcessingJob).GetProperty("AudioClipId").Should().BeNull();
        typeof(AudioProcessingJob).GetProperty("AudioClip").Should().BeNull();

        using var db = CreateDbContext();
        var job = db.Model.FindEntityType(typeof(AudioProcessingJob));

        job.Should().NotBeNull();
        job!.FindProperty(nameof(AudioProcessingJob.AudioResourceId))!
            .IsNullable.Should().BeFalse();
        job.GetForeignKeys().Should().ContainSingle();
        job.GetForeignKeys().Single().PrincipalEntityType.ClrType
            .Should().Be(typeof(AudioResource));
        job.GetForeignKeys().Single().IsRequired.Should().BeTrue();
    }

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
