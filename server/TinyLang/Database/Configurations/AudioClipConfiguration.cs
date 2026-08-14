using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置音频字段、状态、查询索引和审计/source 关系。
/// </summary>
public sealed class AudioClipConfiguration : IEntityTypeConfiguration<AudioClip>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AudioClip> builder)
    {
        builder.ToTable("audio_clips");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Title).HasMaxLength(200).IsRequired();
        builder.Property(value => value.Description).HasMaxLength(2000);
        builder.Property(value => value.Kind)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.ContainerFormat).HasMaxLength(100);
        builder.Property(value => value.SourceCodec).HasMaxLength(64);
        builder.Property(value => value.ProcessingStatus)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.PublicationStatus)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.OutputObjectName).HasMaxLength(512);
        builder.Property(value => value.LastFailureCode).HasMaxLength(100);
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => value.SourceMediaResourceId).IsUnique();
        builder.HasIndex(value => new
        {
            value.CreatedById,
            value.ProcessingStatus,
            value.PublicationStatus,
            value.UpdatedAt,
            value.Id
        });
        builder.HasIndex(value => new
        {
            value.ProcessingStatus,
            value.PublicationStatus,
            value.Id
        });

        builder.HasIndex(value => value.LastEditorId);

        builder.HasOne(value => value.CreatedBy)
            .WithMany(value => value.CreatedAudioClips)
            .HasForeignKey(value => value.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.LastEditor)
            .WithMany(value => value.EditedAudioClips)
            .HasForeignKey(value => value.LastEditorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.SourceMediaResource)
            .WithOne(value => value.SourceAudioClip)
            .HasForeignKey<AudioClip>(value => value.SourceMediaResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
