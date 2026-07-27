using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置视频元数据、状态、查询索引和源资源关系。
/// </summary>
public sealed class VideoConfiguration : IEntityTypeConfiguration<Video>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Video> builder)
    {
        builder.ToTable("videos");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.Property(x => x.OriginalLanguage).HasMaxLength(35).IsRequired();
        builder.Property(x => x.ContainerFormat).HasMaxLength(100);
        builder.Property(x => x.VideoCodec).HasMaxLength(64);
        builder.Property(x => x.AudioCodec).HasMaxLength(64);
        builder.Property(x => x.ProcessingStatus)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.PublicationStatus)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.MasterPlaylistObjectName).HasMaxLength(512);
        builder.Property(x => x.PosterObjectName).HasMaxLength(512);
        builder.Property(x => x.LastFailureCode).HasMaxLength(100);
        builder.Property(x => x.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(x => x.SourceMediaResourceId).IsUnique();
        builder.HasIndex(x => new
        {
            x.OwnerId,
            x.ProcessingStatus,
            x.PublicationStatus,
            x.UpdatedAt,
            x.Id
        });
        builder.HasIndex(x => new
        {
            x.ProcessingStatus,
            x.PublicationStatus,
            x.PublishedAt,
            x.Id
        });

        builder.HasOne(x => x.Owner)
            .WithMany(x => x.OwnedVideos)
            .HasForeignKey(x => x.OwnerId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.SourceMediaResource)
            .WithOne(x => x.SourceVideo)
            .HasForeignKey<Video>(x => x.SourceMediaResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
