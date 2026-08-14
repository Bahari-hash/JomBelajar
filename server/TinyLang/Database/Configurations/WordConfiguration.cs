using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置词条字段、规范化唯一键、查询索引和审计用户关系。
/// </summary>
public sealed class WordConfiguration : IEntityTypeConfiguration<Word>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Word> builder)
    {
        builder.ToTable("words");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Headword).HasMaxLength(200).IsRequired();
        builder.Property(value => value.NormalizedHeadword).HasMaxLength(200).IsRequired();
        builder.Property(value => value.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => value.NormalizedHeadword)
            .IsUnique();
        builder.HasIndex(value => new
        {
            value.Status,
            value.UpdatedAt,
            value.Id
        });
        builder.HasIndex(value => new
        {
            value.Status,
            value.PublishedAt,
            value.Id
        });

        builder.HasOne(value => value.CreatedBy)
            .WithMany(value => value.CreatedWords)
            .HasForeignKey(value => value.CreatedById)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.LastEditor)
            .WithMany(value => value.EditedWords)
            .HasForeignKey(value => value.LastEditorId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
