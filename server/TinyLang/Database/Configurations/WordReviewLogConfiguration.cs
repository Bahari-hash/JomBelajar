using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

public sealed class WordReviewLogConfiguration : IEntityTypeConfiguration<WordReviewLog>
{
    public void Configure(EntityTypeBuilder<WordReviewLog> builder)
    {
        builder.ToTable("word_review_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Rating).HasConversion<string>().HasMaxLength(20);
        builder.Property(x => x.SchedulerVersion).HasMaxLength(100);
        builder.HasIndex(x => new { x.SessionItemId, x.SubmissionStamp }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.WordId, x.RatedAt });
        builder.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        // Preserve historical snapshots if a word is removed; no word FK is intentional.
        builder.HasOne<WordStudySessionItem>().WithMany().HasForeignKey(x => x.SessionItemId).OnDelete(DeleteBehavior.Cascade);
    }
}
