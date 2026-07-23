namespace TinyLang.Interfaces;

using Microsoft.EntityFrameworkCore;
using TinyLang.Entities;

public interface IApplicationDbContext
{
    DbSet<User> Users { get; }
    DbSet<RefreshToken> RefreshTokens { get; }
    DbSet<MediaResource> MediaResources { get; }
    DbSet<Article> Articles { get; }
    DbSet<ArticleCategory> ArticleCategories { get; }
    DbSet<ArticleCategoryAssignment> ArticleCategoryAssignments { get; }
    DbSet<ArticleMediaResource> ArticleMediaResources { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken);
}
