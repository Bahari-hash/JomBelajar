using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置用户视频进度的用户/视频唯一性和查询索引。
/// </summary>
public sealed class UserVideoProgressConfiguration
    : IEntityTypeConfiguration<UserVideoProgress>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserVideoProgress> builder)
    {
        builder.ToTable("user_video_progress");
        builder.HasKey(x => x.Id);
        builder.HasIndex(x => new { x.UserId, x.VideoId }).IsUnique();
        builder.HasIndex(x => new { x.UserId, x.LastPlayedAt });
        builder.HasOne(x => x.User)
            .WithMany(x => x.VideoProgress)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Video)
            .WithMany(x => x.UserProgress)
            .HasForeignKey(x => x.VideoId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
