using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置例句文本、顺序和可选音频关系。
/// </summary>
public sealed class ExampleSentenceConfiguration
    : IEntityTypeConfiguration<ExampleSentence>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ExampleSentence> builder)
    {
        builder.ToTable("example_sentences");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Sentence).HasMaxLength(2000).IsRequired();
        builder.Property(value => value.Translation).HasMaxLength(2000).IsRequired();
        builder.HasIndex(value => new { value.WordSenseId, value.SortOrder }).IsUnique();
        builder.HasOne(value => value.WordSense)
            .WithMany(value => value.Examples)
            .HasForeignKey(value => value.WordSenseId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.AudioClip)
            .WithMany(value => value.ExampleSentences)
            .HasForeignKey(value => value.AudioClipId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
