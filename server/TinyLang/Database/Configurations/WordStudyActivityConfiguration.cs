using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置单词学习活动流水的幂等约束、查询索引和生命周期关系。
/// </summary>
public sealed class WordStudyActivityConfiguration
    : IEntityTypeConfiguration<WordStudyActivity>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordStudyActivity> builder)
    {
        builder.ToTable("word_study_activities");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.ActivityType)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();
        builder.Property(value => value.CompletedAtUtc).IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasIndex(value => new
        {
            value.SessionItemId,
            value.ActivityType
        }).IsUnique();
        builder.HasIndex(value => new
        {
            value.UserId,
            value.CompletedAtUtc,
            value.WordId
        });

        builder.HasOne(value => value.User)
            .WithMany(value => value.WordStudyActivities)
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.Word)
            .WithMany(value => value.WordStudyActivities)
            .HasForeignKey(value => value.WordId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(value => value.Session)
            .WithMany(value => value.Activities)
            .HasForeignKey(value => value.SessionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.SessionItem)
            .WithMany(value => value.Activities)
            .HasForeignKey(value => value.SessionItemId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
