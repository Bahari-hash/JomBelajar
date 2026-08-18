using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置固定会话项的位置、单词唯一性、并发和删除行为。
/// </summary>
public sealed class WordStudySessionItemConfiguration
    : IEntityTypeConfiguration<WordStudySessionItem>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordStudySessionItem> builder)
    {
        builder.ToTable("word_study_session_items", table =>
        {
            table.HasCheckConstraint(
                "CK_word_study_session_items_position",
                "\"Position\" >= 0");
            table.HasCheckConstraint(
                "CK_word_study_session_items_attempt_counts",
                "\"MemorizationAttemptCount\" >= 0 AND \"SpellingAttemptCount\" >= 0 " +
                "AND \"MemorizationQueueOrder\" >= 0 AND \"SpellingQueueOrder\" >= 0");
        });
        builder.HasKey(value => value.Id);
        builder.Property(value => value.Status)
            .HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(value => value.SkipReason)
            .HasConversion<string>().HasMaxLength(32);
        builder.Property(value => value.ConcurrencyStamp).IsConcurrencyToken();
        builder.HasIndex(value => new { value.SessionId, value.WordId }).IsUnique();
        builder.HasIndex(value => new { value.SessionId, value.Position }).IsUnique();
        builder.HasIndex(value => new
            {
                value.SessionId,
                value.Status,
                value.MemorizationQueueOrder
            }, "IX_word_study_items_memorization_queue");
        builder.HasIndex(value => new
            {
                value.SessionId,
                value.Status,
                value.SpellingQueueOrder
            }, "IX_word_study_items_spelling_queue");

        builder.HasOne(value => value.Session)
            .WithMany(value => value.Items)
            .HasForeignKey(value => value.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.Word)
            .WithMany(value => value.StudySessionItems)
            .HasForeignKey(value => value.WordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
