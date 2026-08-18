using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class WordStudyEndpoints
{
    public static RouteGroupBuilder MapWordStudyApi(this RouteGroupBuilder endpoints)
    {
        var learning = endpoints.MapGroup("/word-study/learning")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        learning.MapGet("/overview", GetLearningOverviewAsync);
        learning.MapPost("/sessions", StartLearningAsync);
        learning.MapGet("/sessions/{sessionId:guid}", GetLearningSessionAsync);
        learning.MapGet("/sessions/{sessionId:guid}/results", GetLearningResultsAsync);
        learning.MapPost("/sessions/{sessionId:guid}/items/{itemId:guid}/memorization", SubmitLearningMemorizationAsync);
        learning.MapPost("/sessions/{sessionId:guid}/items/{itemId:guid}/spelling", SubmitLearningSpellingAsync);

        var review = endpoints.MapGroup("/word-study/review")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        review.MapGet("/overview", GetReviewOverviewAsync);
        review.MapPost("/sessions", StartReviewAsync);
        review.MapGet("/sessions/{sessionId:guid}", GetReviewSessionAsync);
        review.MapGet("/sessions/{sessionId:guid}/results", GetReviewResultsAsync);
        review.MapPost("/sessions/{sessionId:guid}/items/{itemId:guid}/memorization", SubmitReviewMemorizationAsync);
        review.MapPost("/sessions/{sessionId:guid}/items/{itemId:guid}/spelling", SubmitReviewSpellingAsync);
        review.MapPost("/sessions/{sessionId:guid}/items/{itemId:guid}/exclude", ExcludeAsync);
        return endpoints;
    }

    public static async Task<Ok<WordLearningOverviewResponse>> GetLearningOverviewAsync(ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetLearningOverviewAsync(EndpointIdentity.GetUserId(principal), ct));
    public static async Task<Created<WordStudySessionStateResponse>> StartLearningAsync(ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
    {
        var response = await service.StartLearningSessionAsync(EndpointIdentity.GetUserId(principal), ct);
        return TypedResults.Created($"/api/word-study/learning/sessions/{response.Id}", response);
    }
    public static async Task<Ok<WordStudySessionStateResponse>> GetLearningSessionAsync(Guid sessionId, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetLearningSessionAsync(EndpointIdentity.GetUserId(principal), sessionId, ct));
    public static async Task<Ok<IReadOnlyList<WordStudyCompletedItemResponse>>> GetLearningResultsAsync(Guid sessionId, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetCompletedSessionItemsAsync(EndpointIdentity.GetUserId(principal), sessionId, WordStudySessionType.Learning, ct));
    public static async Task<Ok<IReadOnlyList<WordStudyCompletedItemResponse>>> GetReviewResultsAsync(Guid sessionId, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetCompletedSessionItemsAsync(EndpointIdentity.GetUserId(principal), sessionId, WordStudySessionType.Review, ct));
    public static async Task<Ok<WordStudyCommandResponse>> SubmitLearningMemorizationAsync(Guid sessionId, Guid itemId, SubmitWordMemorizationRequest request, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.SubmitLearningMemorizationAsync(EndpointIdentity.GetUserId(principal), sessionId, itemId, request, ct));
    public static async Task<Ok<WordStudyCommandResponse>> SubmitLearningSpellingAsync(Guid sessionId, Guid itemId, SubmitWordSpellingRequest request, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.SubmitLearningSpellingAsync(EndpointIdentity.GetUserId(principal), sessionId, itemId, request, ct));

    public static async Task<Ok<WordReviewOverviewResponse>> GetReviewOverviewAsync(ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetReviewOverviewAsync(EndpointIdentity.GetUserId(principal), ct));
    public static async Task<Created<WordStudySessionStateResponse>> StartReviewAsync(ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
    {
        var response = await service.StartReviewSessionAsync(EndpointIdentity.GetUserId(principal), ct);
        return TypedResults.Created($"/api/word-study/review/sessions/{response.Id}", response);
    }
    public static async Task<Ok<WordStudySessionStateResponse>> GetReviewSessionAsync(Guid sessionId, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.GetReviewSessionAsync(EndpointIdentity.GetUserId(principal), sessionId, ct));
    public static async Task<Ok<WordStudyCommandResponse>> SubmitReviewMemorizationAsync(Guid sessionId, Guid itemId, SubmitWordMemorizationRequest request, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.SubmitReviewMemorizationAsync(EndpointIdentity.GetUserId(principal), sessionId, itemId, request, ct));
    public static async Task<Ok<WordStudyCommandResponse>> SubmitReviewSpellingAsync(Guid sessionId, Guid itemId, SubmitWordSpellingRequest request, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.SubmitReviewSpellingAsync(EndpointIdentity.GetUserId(principal), sessionId, itemId, request, ct));
    public static async Task<Ok<WordStudyCommandResponse>> ExcludeAsync(Guid sessionId, Guid itemId, ExcludeWordFromReviewRequest request, ClaimsPrincipal principal, IWordStudyService service, CancellationToken ct)
        => TypedResults.Ok(await service.ExcludeFromReviewAsync(EndpointIdentity.GetUserId(principal), sessionId, itemId, request, ct));
}
