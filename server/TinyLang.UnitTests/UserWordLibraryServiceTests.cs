using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Services;

namespace TinyLang.UnitTests;

public sealed class UserWordLibraryServiceTests
{
    [Fact]
    public async Task FavoriteShouldBeIdempotentAndPaginated()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "library@example.test", PasswordHash = "hash" };
        var word = new Word
        {
            Headword = "favorite",
            NormalizedHeadword = "FAVORITE",
            Senses = [new WordSense { Definition = "definition", SortOrder = 0 }]
        };
        db.AddRange(user, word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = new UserWordLibraryService(
            db,
            new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")));

        await service.SetFavoriteAsync(user.Id, word.Id, true, TestContext.Current.CancellationToken);
        await service.SetFavoriteAsync(user.Id, word.Id, true, TestContext.Current.CancellationToken);
        var page = await service.GetFavoritesAsync(
            user.Id,
            new UserWordLibraryListRequest { Page = 1, PageSize = 20 },
            TestContext.Current.CancellationToken);

        page.TotalCount.Should().Be(1);
        page.Items.Should().ContainSingle().Which.WordId.Should().Be(word.Id);
    }

    [Fact]
    public async Task RestoreReviewShouldResetStageAndScheduleTomorrow()
    {
        await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
        var user = new User { Email = "restore@example.test", PasswordHash = "hash" };
        var word = new Word
        {
            Headword = "restore",
            NormalizedHeadword = "RESTORE",
            Senses = [new WordSense { Definition = "definition", SortOrder = 0 }]
        };
        db.AddRange(user, word);
        db.UserWordProgress.Add(new UserWordProgress
        {
            UserId = user.Id,
            WordId = word.Id,
            ReviewStage = 4,
            IsReviewExcluded = true,
            ReviewExcludedAt = DateTimeOffset.Parse("2026-08-17T03:00:00Z")
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        await new UserWordLibraryService(
            db,
            new TestTimeProvider(DateTimeOffset.Parse("2026-08-18T03:00:00Z")))
            .RestoreReviewAsync(user.Id, word.Id, TestContext.Current.CancellationToken);

        var progress = await db.UserWordProgress.SingleAsync(
            value => value.UserId == user.Id && value.WordId == word.Id,
            TestContext.Current.CancellationToken);
        progress.IsReviewExcluded.Should().BeFalse();
        progress.ReviewStage.Should().Be(0);
        progress.NextReviewAt.Should().Be(DateTimeOffset.Parse("2026-08-19T03:00:00Z"));
    }
}
