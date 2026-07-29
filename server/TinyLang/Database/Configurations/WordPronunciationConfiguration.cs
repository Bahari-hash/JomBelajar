using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置词条发音顺序、默认项和音频引用约束。
/// </summary>
public sealed class WordPronunciationConfiguration
    : IEntityTypeConfiguration<WordPronunciation>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordPronunciation> builder)
    {
        builder.ToTable("word_pronunciations");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.AccentTag).HasMaxLength(100);
        builder.Property(value => value.Ipa).HasMaxLength(200);
        builder.HasIndex(value => new { value.WordId, value.AudioClipId }).IsUnique();
        builder.HasIndex(value => new { value.WordId, value.SortOrder }).IsUnique();
        builder.HasIndex(value => value.WordId)
            .IsUnique()
            .HasFilter("\"IsDefault\" = TRUE");
        builder.HasOne(value => value.Word)
            .WithMany(value => value.Pronunciations)
            .HasForeignKey(value => value.WordId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.AudioClip)
            .WithMany(value => value.WordPronunciations)
            .HasForeignKey(value => value.AudioClipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
