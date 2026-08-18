using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class UserWordLibraryEndpoints
{
    public static RouteGroupBuilder MapUserWordLibraryApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users/me")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        group.MapGet("/word-favorites", GetFavoritesAsync);
        group.MapPut("/word-favorites/{wordId:guid}", AddFavoriteAsync);
        group.MapDelete("/word-favorites/{wordId:guid}", RemoveFavoriteAsync);
        group.MapGet("/word-review-exclusions", GetExclusionsAsync);
        group.MapDelete("/word-review-exclusions/{wordId:guid}", RestoreReviewAsync);
        return endpoints;
    }

    public static async Task<Ok<PagedResponse<UserWordFavoriteResponse>>> GetFavoritesAsync([AsParameters] UserWordLibraryListRequest request, ClaimsPrincipal principal, IUserWordLibraryService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetFavoritesAsync(EndpointIdentity.GetUserId(principal), request, ct));
    public static async Task<NoContent> AddFavoriteAsync(Guid wordId, ClaimsPrincipal principal, IUserWordLibraryService service, CancellationToken ct)
    {
        await service.SetFavoriteAsync(EndpointIdentity.GetUserId(principal), wordId, true, ct);
        return TypedResults.NoContent();
    }
    public static async Task<NoContent> RemoveFavoriteAsync(Guid wordId, ClaimsPrincipal principal, IUserWordLibraryService service, CancellationToken ct)
    {
        await service.SetFavoriteAsync(EndpointIdentity.GetUserId(principal), wordId, false, ct);
        return TypedResults.NoContent();
    }
    public static async Task<Ok<PagedResponse<UserWordReviewExclusionResponse>>> GetExclusionsAsync([AsParameters] UserWordLibraryListRequest request, ClaimsPrincipal principal, IUserWordLibraryService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetReviewExclusionsAsync(EndpointIdentity.GetUserId(principal), request, ct));
    public static async Task<NoContent> RestoreReviewAsync(Guid wordId, ClaimsPrincipal principal, IUserWordLibraryService service, CancellationToken ct)
    {
        await service.RestoreReviewAsync(EndpointIdentity.GetUserId(principal), wordId, ct);
        return TypedResults.NoContent();
    }
}
