using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TinyLang.Entities;

namespace TinyLang.Database.Configurations;

/// <summary>
/// 配置用户单词收藏的唯一性和删除边界。
/// </summary>
public sealed class UserWordFavoriteConfiguration
    : IEntityTypeConfiguration<UserWordFavorite>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<UserWordFavorite> builder)
    {
        builder.ToTable("user_word_favorites");
        builder.HasKey(value => value.Id);
        builder.Property(value => value.CreatedAt).IsRequired();
        builder.HasIndex(value => new { value.UserId, value.WordId }).IsUnique();

        builder.HasOne(value => value.User)
            .WithMany(value => value.WordFavorites)
            .HasForeignKey(value => value.UserId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(value => value.Word)
            .WithMany(value => value.Favorites)
            .HasForeignKey(value => value.WordId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
