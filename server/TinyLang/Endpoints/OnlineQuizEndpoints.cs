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
        var userGroup = endpoints.MapGroup("/")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        userGroup.MapGet("/paper-tags", GetPaperTagsAsync);

        userGroup.MapGet("/papers", GetPapersAsync);
        userGroup.MapGet("/papers/{paperId:guid}", GetPaperAsync);
        userGroup.MapPost("/papers/{paperId:guid}/attempts", StartAttemptAsync);
        userGroup.MapGet("/papers/{paperId:guid}/attempts", GetAttemptHistoryAsync);

        userGroup.MapGet("/paper-attempts/{attemptId:guid}", GetAttemptAsync);
        userGroup.MapPut("/paper-attempts/{attemptId:guid}/answers/{questionId:guid}", SaveAnswerAsync);
        userGroup.MapDelete("/paper-attempts/{attemptId:guid}/answers/{questionId:guid}", ClearAnswerAsync);
        userGroup.MapPost("/paper-attempts/{attemptId:guid}/submit", SubmitAttemptAsync);
        userGroup.MapGet("/paper-attempts/{attemptId:guid}/result", GetAttemptResultAsync);

        var adminGroup = endpoints.MapGroup("/admin")
            .RequireAuthorization(AuthorizationPolicies.RequireAdmin);

        adminGroup.MapGet("/paper-tags", GetAdminPaperTagsAsync);

        adminGroup.MapPost("/papers", CreatePaperAsync);
        adminGroup.MapGet("/papers", GetAdminPapersAsync);
        adminGroup.MapGet("/papers/{paperId:guid}", GetAdminPaperAsync);
        adminGroup.MapPut("/papers/{paperId:guid}", UpdatePaperAsync);
        adminGroup.MapPost("/papers/{paperId:guid}/validate", ValidatePaperAsync);
        adminGroup.MapPost("/papers/{paperId:guid}/publish", PublishPaperAsync);
        adminGroup.MapPost("/papers/{paperId:guid}/unpublish", UnpublishPaperAsync);
        adminGroup.MapPost("/papers/{paperId:guid}/archive", ArchivePaperAsync);
        adminGroup.MapDelete("/papers/{paperId:guid}", DeletePaperAsync);

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
    /// 使用当前客户端版本只读检查试卷发布要求。
    /// </summary>
    public static async Task<Ok<PaperValidationResponse>> ValidatePaperAsync(
        Guid paperId,
        PaperMutationRequest request,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.ValidateAsync(
            paperId,
            request,
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份幂等发布试卷。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> PublishPaperAsync(
        Guid paperId,
        PaperMutationRequest request,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.PublishAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份幂等下架试卷。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> UnpublishPaperAsync(
        Guid paperId,
        PaperMutationRequest request,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.UnpublishAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 以当前管理员身份将可退出试卷迁移到不可恢复终态。
    /// </summary>
    public static async Task<Ok<AdminPaperResponse>> ArchivePaperAsync(
        Guid paperId,
        PaperMutationRequest request,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.ArchiveAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken));

    /// <summary>
    /// 删除当前允许删除的试卷聚合。
    /// </summary>
    public static async Task<NoContent> DeletePaperAsync(
        Guid paperId,
        [FromBody] PaperMutationRequest request,
        ClaimsPrincipal principal,
        IPaperService paperService,
        CancellationToken cancellationToken)
    {
        await paperService.DeleteAsync(
            paperId,
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.NoContent();
    }

    /// <summary>
    /// 获取当前用户可见的已发布试卷标签目录。
    /// </summary>
    public static async Task<Ok<PagedResponse<PaperTagSummaryResponse>>> GetPaperTagsAsync(
        [AsParameters] PaperTagListRequest request,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.GetPublicTagListAsync(
            request,
            cancellationToken));

    /// <summary>
    /// 获取管理员可见的全部试卷标签目录。
    /// </summary>
    public static async Task<Ok<PagedResponse<PaperTagSummaryResponse>>> GetAdminPaperTagsAsync(
        [AsParameters] PaperTagListRequest request,
        IPaperService paperService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await paperService.GetAdminTagListAsync(
            request,
            cancellationToken));
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
    /// 幂等清除活动测验中一道题的已保存答案。
    /// </summary>
    public static async Task<NoContent> ClearAnswerAsync(
        Guid attemptId,
        Guid questionId,
        ClaimsPrincipal principal,
        IPaperAttemptService attemptService,
        CancellationToken cancellationToken)
    {
        await attemptService.ClearAnswerAsync(
            EndpointIdentity.GetUserId(principal),
            attemptId,
            questionId,
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
