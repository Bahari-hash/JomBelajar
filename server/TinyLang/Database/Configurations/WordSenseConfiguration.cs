using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置词条释义字段、顺序唯一性和父词条关系。
/// </summary>
public sealed class WordSenseConfiguration : IEntityTypeConfiguration<WordSense>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordSense> builder)
    {
        builder.ToTable("word_senses");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.PartOfSpeech)
            .HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(value => value.Definition).HasMaxLength(2000).IsRequired();
        builder.Property(value => value.DefinitionLanguageTag).HasMaxLength(35).IsRequired();
        builder.Property(value => value.UsageNote).HasMaxLength(1000);
        builder.HasIndex(value => new { value.WordId, value.SortOrder }).IsUnique();
        builder.HasIndex(value => new { value.PartOfSpeech, value.WordId });
        builder.HasOne(value => value.Word)
            .WithMany(value => value.Senses)
            .HasForeignKey(value => value.WordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
