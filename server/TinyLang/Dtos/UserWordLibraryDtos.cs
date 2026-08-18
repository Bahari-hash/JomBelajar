namespace TinyLang.Dtos;

public sealed record UserWordLibraryListRequest
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 20;
}

public sealed record UserWordFavoriteResponse(
    Guid WordId,
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId,
    DateTimeOffset CreatedAt);

public sealed record UserWordReviewExclusionResponse(
    Guid WordId,
    string Headword,
    IReadOnlyList<WordSenseResponse> Senses,
    Guid? AudioResourceId,
    DateTimeOffset ExcludedAt);
