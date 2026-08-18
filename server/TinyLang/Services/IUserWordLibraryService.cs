using TinyLang.Dtos;

namespace TinyLang.Services;

public interface IUserWordLibraryService
{
    Task<PagedResponse<UserWordFavoriteResponse>> GetFavoritesAsync(Guid userId, UserWordLibraryListRequest request, CancellationToken cancellationToken = default);
    Task SetFavoriteAsync(Guid userId, Guid wordId, bool favorite, CancellationToken cancellationToken = default);
    Task<PagedResponse<UserWordReviewExclusionResponse>> GetReviewExclusionsAsync(Guid userId, UserWordLibraryListRequest request, CancellationToken cancellationToken = default);
    Task RestoreReviewAsync(Guid userId, Guid wordId, CancellationToken cancellationToken = default);
}
