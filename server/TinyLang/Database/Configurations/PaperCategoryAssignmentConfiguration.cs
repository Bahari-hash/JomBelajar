using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

public sealed class PaperCategoryAssignmentConfiguration
    : IEntityTypeConfiguration<PaperCategoryAssignment>
{
    public void Configure(EntityTypeBuilder<PaperCategoryAssignment> builder)
    {
        builder.ToTable("paper_category_assignments");
        builder.HasKey(x => new { x.PaperId, x.PaperCategoryId });
        builder.HasIndex(x => new { x.PaperCategoryId, x.PaperId });
        builder.HasOne(x => x.Paper)
            .WithMany(x => x.CategoryAssignments)
            .HasForeignKey(x => x.PaperId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.PaperCategory)
            .WithMany(x => x.PaperAssignments)
            .HasForeignKey(x => x.PaperCategoryId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
