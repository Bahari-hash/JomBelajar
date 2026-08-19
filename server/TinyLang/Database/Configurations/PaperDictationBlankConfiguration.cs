using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

public sealed class PaperDictationBlankConfiguration
    : IEntityTypeConfiguration<PaperDictationBlank>
{
    public void Configure(EntityTypeBuilder<PaperDictationBlank> builder)
    {
        builder.ToTable("paper_dictation_blanks", table =>
            table.HasCheckConstraint("CK_paper_dictation_blanks_sort_order",
                "\"SortOrder\" BETWEEN 0 AND 10000"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Answer).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.NormalizedAnswer).HasMaxLength(1000).IsRequired();
        builder.HasIndex(x => new { x.QuestionId, x.SortOrder }).IsUnique();
        builder.HasIndex(x => new { x.QuestionId, x.NormalizedAnswer }).IsUnique();
        builder.HasOne(x => x.Question)
            .WithMany(x => x.DictationBlanks)
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
