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
/// 定义管理员试卷管理和登录用户在线测验 HTTP endpoints。
/// </summary>
public static class OnlineQuizEndpoints
{
    /// <summary>
    /// 注册试卷编辑、用户目录和用户测验路由及其授权策略。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapOnlineQuizApi(this RouteGroupBuilder endpoints)
    {
        var admin = endpoints.MapGroup("/admin/papers")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);
        admin.MapPost("", CreatePaperAsync);
        admin.MapGet("", GetAdminPapersAsync);
        admin.MapGet("/{paperId:guid}", GetAdminPaperAsync);
        admin.MapPut("/{paperId:guid}", UpdatePaperAsync);
        admin.MapPost("/{paperId:guid}/publish", PublishPaperAsync);
        admin.MapPost("/{paperId:guid}/unpublish", UnpublishPaperAsync);
        admin.MapDelete("/{paperId:guid}", DeletePaperAsync);

        var papers = endpoints.MapGroup("/papers")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        papers.MapGet("", GetPapersAsync);
        papers.MapGet("/{paperId:guid}", GetPaperAsync);
        papers.MapPost("/{paperId:guid}/attempts", StartAttemptAsync);
        papers.MapGet("/{paperId:guid}/attempts", GetAttemptHistoryAsync);

        var attempts = endpoints.MapGroup("/paper-attempts")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        attempts.MapGet("/{attemptId:guid}", GetAttemptAsync);
        attempts.MapPut(
            "/{attemptId:guid}/answers/{questionId:guid}",
            SaveAnswerAsync);
        attempts.MapPost("/{attemptId:guid}/submit", SubmitAttemptAsync);
        attempts.MapGet("/{attemptId:guid}/result", GetAttemptResultAsync);
        return endpoints;
    }

    /// <summary>
    /// 以当前管理员身份创建试卷草稿。
    /// </summary>
    public static async Task<Created<AdminPaperResponse>> CreatePaperAsync(
        CreatePaperRequest request,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
    {
        var response = await paperService.CreateDraftAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created(
            $"/api/admin/papers/{response.Id}",
            response);
    }

    /// <summary>
    /// 获取管理员可见的全局试卷分页列表。
    /// </summary>
    public static async Task<Ok<PagedResponse<AdminPaperListItemResponse>>>
        GetAdminPapersAsync(
            [AsParameters] AdminPaperListRequest request,
            IPaperService paperService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.GetAdminListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 获取管理员可见的完整试卷及标准答案。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> GetAdminPaperAsync(
        Guid paperId,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.GetAdminByIdAsync(
            paperId,
            cancellationToken));

    /// <summary>
    /// 以完整目标集合和并发标识更新试卷。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> UpdatePaperAsync(
        Guid paperId,
        UpdatePaperRequest request,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.UpdateAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份幂等发布试卷。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> PublishPaperAsync(
        Guid paperId,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.PublishAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份幂等下架试卷。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> UnpublishPaperAsync(
        Guid paperId,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.UnpublishAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            cancellationToken));

    /// <summary>
    /// 删除当前允许删除的试卷聚合。
    /// </summary>
    public static async Task<NoContent> DeletePaperAsync(
        Guid paperId,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
    {
        await paperService.DeleteAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 获取当前用户可见的已发布试卷分页目录。
    /// </summary>
    public static async Task<Ok<PagedResponse<PaperCatalogItemResponse>>> GetPapersAsync(
        [AsParameters] PaperCatalogRequest request,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.GetCatalogAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 获取当前用户可见的已发布试卷安全详情。
    /// </summary>
    public static async Task<Ok<PaperDetailsResponse>> GetPaperAsync(
        Guid paperId,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.GetDetailsAsync(
            paperId,
            cancellationToken));

    /// <summary>
    /// 创建新测验时返回 201，恢复既有活动测验时返回 200。
    /// </summary>
    public static async Task<Results<Created<UserPaperAttemptResponse>,
        Ok<UserPaperAttemptResponse>>> StartAttemptAsync(
        Guid paperId,
        ClaimsPrincipal principal,
        IPaperAttemptService attemptService,
        CancellationToken cancellationToken)
    {
        var outcome = await attemptService.StartAsync(
            EndpointIdentity.GetUserId(principal),
            paperId,
            cancellationToken);
        return outcome.WasCreated
            ? TypedResults.Created(
                $"/api/paper-attempts/{outcome.Attempt.Id}",
                outcome.Attempt)
            : TypedResults.Ok(outcome.Attempt);
    }

    /// <summary>
    /// 获取当前用户针对指定试卷的有界测验历史。
    /// </summary>
    public static async Task<Ok<PagedResponse<PaperAttemptSummaryResponse>>>
        GetAttemptHistoryAsync(
            Guid paperId,
            [AsParameters] PaperAttemptListRequest request,
            ClaimsPrincipal principal,
            IPaperAttemptService attemptService,
            CancellationToken cancellationToken)
        => TypedResults.Ok(await attemptService.GetHistoryAsync(
            EndpointIdentity.GetUserId(principal),
            paperId,
            request,
            cancellationToken));

    /// <summary>
    /// 获取当前用户拥有的可恢复测验和已保存答案。
    /// </summary>
    public static async Task<Ok<UserPaperAttemptResponse>> GetAttemptAsync(
        Guid attemptId,
        ClaimsPrincipal principal,
        IPaperAttemptService attemptService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await attemptService.GetAttemptAsync(
            EndpointIdentity.GetUserId(principal),
            attemptId,
            cancellationToken));

    /// <summary>
    /// 幂等新增或覆盖活动测验中的一道题答案。
    /// </summary>
    public static async Task<NoContent> SaveAnswerAsync(
        Guid attemptId,
        Guid questionId,
        SavePaperAttemptAnswerRequest request,
        ClaimsPrincipal principal,
        IPaperAttemptService attemptService,
        CancellationToken cancellationToken)
    {
        await attemptService.SaveAnswerAsync(
            EndpointIdentity.GetUserId(principal),
            attemptId,
            questionId,
            request,
            cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 原子提交并返回测验结果，重复提交返回同一持久化结果。
    /// </summary>
    public static async Task<Ok<PaperAttemptResultResponse>> SubmitAttemptAsync(
        Guid attemptId,
        ClaimsPrincipal principal,
        IPaperAttemptService attemptService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await attemptService.SubmitAsync(
            EndpointIdentity.GetUserId(principal),
            attemptId,
            cancellationToken));

    /// <summary>
    /// 获取当前用户已经提交的稳定测验结果。
    /// </summary>
    public static async Task<Ok<PaperAttemptResultResponse>> GetAttemptResultAsync(
        Guid attemptId,
        ClaimsPrincipal principal,
        IPaperAttemptService attemptService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await attemptService.GetResultAsync(
            EndpointIdentity.GetUserId(principal),
            attemptId,
            cancellationToken));
}
