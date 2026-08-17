using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义管理员词条管理和登录用户词条查询 HTTP endpoints。
/// </summary>
public static class WordEndpoints
{
    public static RouteGroupBuilder MapWordsApi(this RouteGroupBuilder endpoints)
    {
        var userGroup = endpoints.MapGroup("/")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        userGroup.MapGet("/words", GetWordsAsync);
        userGroup.MapGet("/words/{id:guid}", GetWordAsync);

        var adminGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapPost("/words/batch/validate", ValidateWordBatchAsync);
        adminGroup.MapPost("/words/batch", ImportWordBatchAsync);
        adminGroup.MapPost("/words", CreateWordAsync);
        adminGroup.MapGet("/words", GetAdminWordsAsync);
        adminGroup.MapGet("/words/{id:guid}", GetAdminWordAsync);
        adminGroup.MapPost("/words/{id:guid}", RejectUnsupportedWordPost)
            .ExcludeFromDescription();
        adminGroup.MapPut("/words/{id:guid}", UpdateWordAsync);
        adminGroup.MapDelete("/words/{id:guid}", DeleteWordAsync);

        return endpoints;
    }

    [RequestSizeLimit(WordConstraints.MaxBatchRequestBodyBytes)]
    public static async Task<Ok<BatchWordValidationResponse>> ValidateWordBatchAsync(
        BatchWordRequest request,
        IWordBatchService service,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await service.ValidateAsync(request, cancellationToken));

    [RequestSizeLimit(WordConstraints.MaxBatchRequestBodyBytes)]
    public static async Task<Results<
        Ok<BatchWordImportResponse>,
        UnprocessableEntity<BatchWordValidationResponse>>> ImportWordBatchAsync(
        BatchWordRequest request,
        ClaimsPrincipal principal,
        IWordBatchService service,
        CancellationToken cancellationToken)
    {
        var result = await service.ImportAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return result.Imported is { } imported
            ? TypedResults.Ok(imported)
            : TypedResults.UnprocessableEntity(result.Validation!);
    }

    public static async Task<Created<AdminWordResponse>> CreateWordAsync(
        CreateWordRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
    {
        var response = await wordService.CreateAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/admin/words/{response.Id}", response);
    }

    public static async Task<Ok<PagedResponse<AdminWordListItemResponse>>>
        GetAdminWordsAsync(
            [AsParameters] AdminWordListRequest request,
            IWordService wordService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetAdminListAsync(
            request,
            cancellationToken));

    public static async Task<Ok<AdminWordResponse>> GetAdminWordAsync(
        Guid id,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetAdminByIdAsync(id, cancellationToken));

    public static NotFound RejectUnsupportedWordPost() => TypedResults.NotFound();

    public static async Task<Ok<AdminWordResponse>> UpdateWordAsync(
        Guid id,
        UpdateWordRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.UpdateAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    public static async Task<NoContent> DeleteWordAsync(
        Guid id,
        [FromBody] DeleteWordRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
    {
        await wordService.DeleteAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Ok<PagedResponse<WordListItemResponse>>> GetWordsAsync(
        [AsParameters] WordListRequest request,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetUserListAsync(request, cancellationToken));

    public static async Task<Ok<WordResponse>> GetWordAsync(
        Guid id,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetUserByIdAsync(id, cancellationToken));
}
