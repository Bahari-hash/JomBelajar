using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Entities;
using TinyLang.Entities.Enums;

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

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);
}
