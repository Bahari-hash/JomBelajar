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
    /// <summary>
    /// 注册词条管理和用户查询路由及其授权策略。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapWordsApi(this RouteGroupBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/words")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        admin.MapPost("", CreateWordAsync);
        admin.MapPost("/batch/validate", ValidateBatchAsync);
        admin.MapPost("/batch", ImportBatchAsync);
        admin.MapGet("", GetAdminWordsAsync);
        admin.MapGet("/{id:guid}", GetAdminWordAsync);
        admin.MapPut("/{id:guid}", UpdateWordAsync);
        admin.MapPost("/{id:guid}/publish", PublishWordAsync);
        admin.MapPost("/{id:guid}/unpublish", UnpublishWordAsync);
        admin.MapPost("/{id:guid}/archive", ArchiveWordAsync);
        admin.MapDelete("/{id:guid}", DeleteWordAsync);

        var user = endpoints.MapGroup("/words")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        user.MapGet("", GetWordsAsync);
        user.MapGet("/{id:guid}", GetWordAsync);
        return endpoints;
    }

    /// <summary>
    /// 以当前管理员身份创建词条草稿。
    /// </summary>
    public static async Task<Created<AdminWordResponse>> CreateWordAsync(
        CreateWordRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
    {
        var response = await wordService.CreateDraftAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created($"/api/admin/words/{response.Id}", response);
    }

    /// <summary>
    /// 获取管理员可见的全局词条分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<AdminWordListItemResponse>>>
        GetAdminWordsAsync(
            [AsParameters] AdminWordListRequest request,
            IWordService wordService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetAdminListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 获取管理员可见的词条管理详情。
    /// </summary>
    public static async Task<Ok<AdminWordResponse>> GetAdminWordAsync(
        Guid id,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.GetAdminByIdAsync(id, cancellationToken));

    /// <summary>
    /// 以完整目标集合和并发标识更新词条。
    /// </summary>
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

    /// <summary>
    /// 以当前管理员身份幂等发布词条。
    /// </summary>
    public static async Task<Ok<AdminWordResponse>> PublishWordAsync(
        Guid id,
        WordMutationRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.PublishAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份幂等下架词条。
    /// </summary>
    public static async Task<Ok<AdminWordResponse>> UnpublishWordAsync(
        Guid id,
        WordMutationRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.UnpublishAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份将可归档词条迁移到不可恢复终态。
    /// </summary>
    public static async Task<Ok<AdminWordResponse>> ArchiveWordAsync(
        Guid id,
        WordMutationRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.ArchiveAsync(
            id,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 删除当前允许删除的词条聚合。
    /// </summary>
    public static async Task<NoContent> DeleteWordAsync(
        Guid id,
        [FromBody] WordMutationRequest request,
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

    /// <summary>
    /// 返回批量新词条的规范化预览和逐行字段错误，不执行写入。
    /// </summary>
    [RequestSizeLimit(WordConstraints.MaxBatchRequestBodyBytes)]
    public static async Task<Ok<BatchWordValidationResponse>> ValidateBatchAsync(
        BatchWordRequest request,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.ValidateBatchAsync(request, cancellationToken));

    /// <summary>
    /// 以当前管理员身份原子创建一组词条草稿。
    /// </summary>
    [RequestSizeLimit(WordConstraints.MaxBatchRequestBodyBytes)]
    public static async Task<Ok<BatchWordImportResponse>> ImportBatchAsync(
        BatchWordRequest request,
        ClaimsPrincipal principal,
        IWordService wordService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await wordService.ImportBatchAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

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
