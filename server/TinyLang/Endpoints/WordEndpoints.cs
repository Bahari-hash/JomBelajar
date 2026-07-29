using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

/// <summary>
/// 定义编辑者词条管理和登录用户词条查询 HTTP endpoints。
/// </summary>
public static class WordEndpoints
{
    /// <summary>
    /// 注册词条管理和用户查询路由及其授权策略。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapWordsApi(this RouteGroupBuilder endpoints)
    {
        var editor = endpoints.MapGroup("/editor/words")
            .RequireAuthorization(AuthorizationPolicies.RequireEditor);
        editor.MapPost("", CreateWordAsync);
        editor.MapGet("", GetEditorWordsAsync);
        editor.MapGet("/{id:guid}", GetEditorWordAsync);
        editor.MapPut("/{id:guid}", UpdateWordAsync);
        editor.MapPost("/{id:guid}/publish", PublishWordAsync);
        editor.MapPost("/{id:guid}/unpublish", UnpublishWordAsync);
        editor.MapDelete("/{id:guid}", DeleteWordAsync);

        var user = endpoints.MapGroup("/words")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        user.MapGet("", GetWordsAsync);
        user.MapGet("/{id:guid}", GetWordAsync);
        return endpoints;
    }

    /// <summary>
    /// 以当前编辑者身份创建词条草稿。
    /// </summary>
    public static async Task<Created<EditorWordResponse>> CreateWordAsync(
        CreateWordRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
    {
        var response = await wordService.CreateDraftAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/editor/words/{response.Id}", response);
    }

    /// <summary>
    /// 获取编辑者可见的全局词条分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<EditorWordListItemResponse>>>
        GetEditorWordsAsync(
            [AsParameters] EditorWordListRequest request,
            IWordService wordService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetEditorListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 获取编辑者可见的词条管理详情。
    /// </summary>
    public static async Task<Ok<EditorWordResponse>> GetEditorWordAsync(
        Guid id,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetEditorByIdAsync(id, cancellationToken));

    /// <summary>
    /// 以完整目标集合和并发标识更新词条。
    /// </summary>
    public static async Task<Ok<EditorWordResponse>> UpdateWordAsync(
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

    /// <summary>
    /// 以当前编辑者身份幂等发布词条。
    /// </summary>
    public static async Task<Ok<EditorWordResponse>> PublishWordAsync(
        Guid id,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.PublishAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 以当前编辑者身份幂等下架词条。
    /// </summary>
    public static async Task<Ok<EditorWordResponse>> UnpublishWordAsync(
        Guid id,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.UnpublishAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 删除当前允许删除的词条聚合。
    /// </summary>
    public static async Task<NoContent> DeleteWordAsync(
        Guid id,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
    {
        await wordService.DeleteAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 获取当前可用的已发布词条分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<WordListItemResponse>>> GetWordsAsync(
        [AsParameters] WordListRequest request,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetUserListAsync(request, cancellationToken));

    /// <summary>
    /// 获取当前可用的已发布词条详情。
    /// </summary>
    public static async Task<Ok<WordResponse>> GetWordAsync(
        Guid id,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetUserByIdAsync(id, cancellationToken));
}
