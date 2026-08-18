using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置词条字段、规范化唯一键、查询索引和共享音频关系。
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
        builder.Property(value => value.StudyOrder)
            .HasDefaultValueSql("nextval('word_study_order_seq')")
            .ValueGeneratedOnAdd();
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();

        builder.HasIndex(value => value.NormalizedHeadword)
            .IsUnique();
        builder.HasIndex(value => value.StudyOrder).IsUnique();
        builder.HasIndex(value => value.AudioResourceId);
        builder.HasIndex(value => new
        {
            value.UpdatedAt,
            value.Id
        });
        builder.HasOne(value => value.AudioResource)
            .WithMany()
            .HasForeignKey(value => value.AudioResourceId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
