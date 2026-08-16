using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置共享音频资源字段、名称唯一约束和资源生命周期关系。
/// </summary>
public sealed class AudioResourceConfiguration : IEntityTypeConfiguration<AudioResource>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AudioResource> builder)
    {
        builder.ToTable("audio_resources");
        builder.HasKey(value => value.Id);

        builder.Property(value => value.Name)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(value => value.NormalizedName)
            .HasMaxLength(255)
            .IsRequired();
        builder.Property(value => value.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(value => value.ContainerFormat).HasMaxLength(100);
        builder.Property(value => value.SourceCodec).HasMaxLength(64);
        builder.Property(value => value.OutputObjectName).HasMaxLength(512);
        builder.Property(value => value.LastFailureCode).HasMaxLength(100);
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => value.NormalizedName).IsUnique();
        builder.HasIndex(value => new
        {
            value.Status,
            value.UpdatedAt,
            value.Id
        });

        builder.HasOne(value => value.CreatedBy)
            .WithMany(value => value.CreatedAudioResources)
            .HasForeignKey(value => value.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.LastEditor)
            .WithMany(value => value.EditedAudioResources)
            .HasForeignKey(value => value.LastEditorId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.SourceMediaResource)
            .WithOne(value => value.SourceAudioResource)
            .HasForeignKey<AudioResource>(value => value.SourceMediaResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
