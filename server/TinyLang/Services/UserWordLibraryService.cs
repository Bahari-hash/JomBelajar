using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Policies;

namespace TinyLang.Services;

public sealed class UserWordLibraryService(
    IApplicationDbContext db,
    IDatabaseExceptionClassifier databaseExceptionClassifier,
    TimeProvider timeProvider) : IUserWordLibraryService
{
    private const string FavoriteUniqueIndex =
        "IX_user_word_favorites_UserId_WordId";

    public async Task<PagedResponse<UserWordFavoriteResponse>> GetFavoritesAsync(Guid userId, UserWordLibraryListRequest request, CancellationToken cancellationToken = default)
    {
        var query = from favorite in db.UserWordFavorites.AsNoTracking()
                    join word in WordVisibilityPolicy.Apply(db.Words.AsNoTracking()) on favorite.WordId equals word.Id
                    where favorite.UserId == userId
                    orderby favorite.CreatedAt descending, favorite.WordId
                    select new UserWordFavoriteResponse(
                        word.Id,
                        word.Headword,
                        word.Senses.OrderBy(value => value.SortOrder)
                            .ThenBy(value => value.Id)
                            .Select(value => new WordSenseResponse(
                                value.PartOfSpeech,
                                value.Definition,
                                value.UsageNote,
                                value.SortOrder,
                                value.Examples.OrderBy(example => example.SortOrder)
                                    .ThenBy(example => example.Id)
                                    .Select(example => new ExampleSentenceResponse(
                                        example.Sentence,
                                        example.Translation,
                                        example.AudioResourceId,
                                        example.SortOrder))
                                    .ToList()))
                            .ToList(),
                        word.AudioResourceId,
                        favorite.CreatedAt);
        return await ToPagedAsync(query, request, cancellationToken);
    }

    public async Task SetFavoriteAsync(Guid userId, Guid wordId, bool favorite, CancellationToken cancellationToken = default)
    {
        var existing = await db.UserWordFavorites.SingleOrDefaultAsync(value => value.UserId == userId && value.WordId == wordId, cancellationToken);
        if (favorite &&
            !await WordVisibilityPolicy.Apply(db.Words.AsNoTracking())
                .AnyAsync(value => value.Id == wordId, cancellationToken))
            throw NotFoundException.Create(ErrorCodes.WordFavoriteWordNotFound);
        if (favorite && existing is null)
        {
            db.UserWordFavorites.Add(new UserWordFavorite { UserId = userId, WordId = wordId, CreatedAt = timeProvider.GetUtcNow() });
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateException exception) when (
                databaseExceptionClassifier.IsUniqueConstraintViolation(
                    exception,
                    FavoriteUniqueIndex))
            {
                db.ClearTrackedChanges();
            }
        }
        else if (!favorite && existing is not null)
        {
            db.UserWordFavorites.Remove(existing);
            try
            {
                await db.SaveChangesAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                db.ClearTrackedChanges();
            }
        }
    }

    public async Task<PagedResponse<UserWordReviewExclusionResponse>> GetReviewExclusionsAsync(Guid userId, UserWordLibraryListRequest request, CancellationToken cancellationToken = default)
    {
        var query = from progress in db.UserWordProgress.AsNoTracking()
                    join word in WordVisibilityPolicy.Apply(db.Words.AsNoTracking()) on progress.WordId equals word.Id
                    where progress.UserId == userId && progress.IsReviewExcluded && progress.ReviewExcludedAt != null
                    orderby progress.ReviewExcludedAt descending, progress.WordId
                    select new UserWordReviewExclusionResponse(
                        word.Id,
                        word.Headword,
                        word.Senses.OrderBy(value => value.SortOrder)
                            .ThenBy(value => value.Id)
                            .Select(value => new WordSenseResponse(
                                value.PartOfSpeech,
                                value.Definition,
                                value.UsageNote,
                                value.SortOrder,
                                value.Examples.OrderBy(example => example.SortOrder)
                                    .ThenBy(example => example.Id)
                                    .Select(example => new ExampleSentenceResponse(
                                        example.Sentence,
                                        example.Translation,
                                        example.AudioResourceId,
                                        example.SortOrder))
                                    .ToList()))
                            .ToList(),
                        word.AudioResourceId,
                        progress.ReviewExcludedAt!.Value);
        return await ToPagedAsync(query, request, cancellationToken);
    }

    public async Task RestoreReviewAsync(Guid userId, Guid wordId, CancellationToken cancellationToken = default)
    {
        var progress = await db.UserWordProgress.SingleOrDefaultAsync(value => value.UserId == userId && value.WordId == wordId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.WordReviewProgressNotFound);
        if (!progress.IsReviewExcluded)
        {
            return;
        }
        var schedule = WordStudySchedule.AfterInitialLearning(timeProvider.GetUtcNow());
        progress.IsReviewExcluded = false;
        progress.ReviewExcludedAt = null;
        progress.ReviewStage = schedule.Stage;
        progress.NextReviewAt = schedule.NextReviewAt;
        progress.ConcurrencyStamp = Guid.NewGuid();
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ClearTrackedChanges();
            // A concurrent restore is already the desired final state.
        }
    }

    private static async Task<PagedResponse<T>> ToPagedAsync<T>(IQueryable<T> query, UserWordLibraryListRequest request, CancellationToken cancellationToken)
    {
        var total = await query.CountAsync(cancellationToken);
        var items = await query.Skip((request.Page - 1) * request.PageSize).Take(request.PageSize).ToListAsync(cancellationToken);
        return new PagedResponse<T>(items, request.Page, request.PageSize, total, total == 0 ? 0 : (int)Math.Ceiling(total / (double)request.PageSize));
    }
}
