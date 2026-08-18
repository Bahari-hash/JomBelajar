using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

public sealed class UserWordLibraryService(IApplicationDbContext db, TimeProvider timeProvider) : IUserWordLibraryService
{
    public async Task<PagedResponse<UserWordFavoriteResponse>> GetFavoritesAsync(Guid userId, UserWordLibraryListRequest request, CancellationToken cancellationToken = default)
    {
        var query = from favorite in db.UserWordFavorites.AsNoTracking()
                    join word in WordVisibilityPolicy.Apply(db.Words.AsNoTracking()) on favorite.WordId equals word.Id
                    where favorite.UserId == userId
                    orderby favorite.CreatedAt descending, favorite.WordId
                    select new UserWordFavoriteResponse(word.Id, word.Headword, ProjectSenses(word).ToArray(), word.AudioResourceId, favorite.CreatedAt);
        return await ToPagedAsync(query, request, cancellationToken);
    }

    public async Task SetFavoriteAsync(Guid userId, Guid wordId, bool favorite, CancellationToken cancellationToken = default)
    {
        if (!await WordVisibilityPolicy.Apply(db.Words.AsNoTracking()).AnyAsync(value => value.Id == wordId, cancellationToken))
            throw NotFoundException.Create(ErrorCodes.WordFavoriteWordNotFound);
        var existing = await db.UserWordFavorites.SingleOrDefaultAsync(value => value.UserId == userId && value.WordId == wordId, cancellationToken);
        if (favorite && existing is null)
        {
            db.UserWordFavorites.Add(new UserWordFavorite { UserId = userId, WordId = wordId, CreatedAt = timeProvider.GetUtcNow() });
            await db.SaveChangesAsync(cancellationToken);
        }
        else if (!favorite && existing is not null)
        {
            db.UserWordFavorites.Remove(existing);
            await db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<PagedResponse<UserWordReviewExclusionResponse>> GetReviewExclusionsAsync(Guid userId, UserWordLibraryListRequest request, CancellationToken cancellationToken = default)
    {
        var query = from progress in db.UserWordProgress.AsNoTracking()
                    join word in WordVisibilityPolicy.Apply(db.Words.AsNoTracking()) on progress.WordId equals word.Id
                    where progress.UserId == userId && progress.IsReviewExcluded && progress.ReviewExcludedAt != null
                    orderby progress.ReviewExcludedAt descending, progress.WordId
                    select new UserWordReviewExclusionResponse(word.Id, word.Headword, ProjectSenses(word).ToArray(), word.AudioResourceId, progress.ReviewExcludedAt!.Value);
        return await ToPagedAsync(query, request, cancellationToken);
    }

    public async Task RestoreReviewAsync(Guid userId, Guid wordId, CancellationToken cancellationToken = default)
    {
        var progress = await db.UserWordProgress.SingleOrDefaultAsync(value => value.UserId == userId && value.WordId == wordId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordReviewProgressNotFound);
        var schedule = WordStudySchedule.AfterInitialLearning(timeProvider.GetUtcNow());
        progress.IsReviewExcluded = false;
        progress.ReviewExcludedAt = null;
        progress.ReviewStage = schedule.Stage;
        progress.NextReviewAt = schedule.NextReviewAt;
        progress.ConcurrencyStamp = Guid.NewGuid();
        await db.SaveChangesAsync(cancellationToken);
    }

    private static IQueryable<WordSenseResponse> ProjectSenses(Word word)
        => word.Senses.AsQueryable().OrderBy(value => value.SortOrder).Select(value => new WordSenseResponse(
            value.PartOfSpeech, value.Definition, value.UsageNote, value.SortOrder,
            value.Examples.OrderBy(example => example.SortOrder).Select(example => new ExampleSentenceResponse(
                example.Sentence, example.Translation, example.AudioResourceId, example.SortOrder)).ToArray()));

    private static async Task<PagedResponse<T>> ToPagedAsync<T>(IQueryable<T> query, UserWordLibraryListRequest request, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, request.Page, request.PageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize));
    }
}
