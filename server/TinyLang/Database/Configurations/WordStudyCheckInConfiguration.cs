using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置用户 UTC 自然日打卡的幂等约束和用户生命周期关系。
/// </summary>
public sealed class WordStudyCheckInConfiguration
    : IEntityTypeConfiguration<WordStudyCheckIn>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<WordStudyCheckIn> builder)
    {
        builder.ToTable("word_study_check_ins");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.StudyDateUtc).IsRequired();
        builder.Property(value => value.CheckedInAtUtc).IsRequired();
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasIndex(value => new
        {
            value.UserId,
            value.StudyDateUtc
        }).IsUnique();

        builder.HasOne(value => value.User)
            .WithMany(value => value.WordStudyCheckIns)
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
