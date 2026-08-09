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
/// 定义登录用户创建、恢复和推进基础单词背诵会话的 HTTP endpoints。
/// </summary>
public static class WordStudyEndpoints
{
    /// <summary>
    /// 注册基础单词背诵路由并统一应用登录用户授权策略。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapWordStudyApi(this RouteGroupBuilder endpoints)
    {
        var study = endpoints.MapGroup("/word-study")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);
        study.MapPost("/sessions", CreateSessionAsync);
        study.MapGet("/today", GetTodayAsync);
        study.MapPost("/today/start", StartTodayAsync);
        study.MapGet("/sessions/active", GetActiveSessionAsync);
        study.MapGet("/sessions/{sessionId:guid}", GetSessionAsync);
        study.MapGet("/sessions/{sessionId:guid}/next", GetNextItemAsync);
        study.MapPost(
            "/sessions/{sessionId:guid}/items/{itemId:guid}/result",
            SubmitResultAsync);
        study.MapPost("/sessions/{sessionId:guid}/abandon", AbandonSessionAsync);
        return endpoints;
    }

    /// <summary>
    /// 获取当前登录用户今天的 UTC 背诵状态。
    /// </summary>
    public static async Task<Ok<WordStudyTodayResponse>> GetTodayAsync(
        ClaimsPrincipal principal,
        IWordStudyService studyService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await studyService.GetTodayAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken));

    /// <summary>
    /// 幂等启动或恢复当前登录用户今天的 UTC 背诵会话。
    /// </summary>
    public static async Task<Created<WordStudySessionResponse>> StartTodayAsync(
        ClaimsPrincipal principal,
        IWordStudyService studyService,
        CancellationToken cancellationToken)
    {
        var response = await studyService.StartTodayAsync(
            EndpointIdentity.GetUserId(principal), cancellationToken);
        return TypedResults.Created($"/api/word-study/sessions/{response.Id}", response);
    }

    /// <summary>
    /// 以当前登录用户身份创建固定内容的背诵会话。
    /// </summary>
    public static async Task<Created<WordStudySessionResponse>> CreateSessionAsync(
        CreateWordStudySessionRequest request,
        ClaimsPrincipal principal,
        IWordStudyService studyService,
        CancellationToken cancellationToken)
    {
        var response = await studyService.CreateSessionAsync(
            EndpointIdentity.GetUserId(principal),
            request,
            cancellationToken);
        return TypedResults.Created(
            $"/api/word-study/sessions/{response.Id}",
            response);
    }

    /// <summary>
    /// 获取当前登录用户可恢复的活动会话或返回无内容。
    /// </summary>
    public static async Task<Results<Ok<WordStudySessionResponse>, NoContent>>
        GetActiveSessionAsync(
            ClaimsPrincipal principal,
            IWordStudyService studyService,
            CancellationToken cancellationToken)
    {
        var response = await studyService.GetActiveSessionAsync(
            EndpointIdentity.GetUserId(principal),
            cancellationToken);
        return response is null
            ? TypedResults.NoContent()
            : TypedResults.Ok(response);
    }

    /// <summary>
    /// 获取当前登录用户拥有的指定会话摘要。
    /// </summary>
    public static async Task<Ok<WordStudySessionResponse>> GetSessionAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        IWordStudyService studyService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await studyService.GetSessionAsync(
            EndpointIdentity.GetUserId(principal),
            sessionId,
            cancellationToken));

    /// <summary>
    /// 获取会话中下一个仍可见的待背诵词条或完成空结果。
    /// </summary>
    public static async Task<Results<Ok<WordStudyNextItemResponse>, NoContent>>
        GetNextItemAsync(
            Guid sessionId,
            ClaimsPrincipal principal,
            IWordStudyService studyService,
            CancellationToken cancellationToken)
    {
        var response = await studyService.GetNextItemAsync(
            EndpointIdentity.GetUserId(principal),
            sessionId,
            cancellationToken);
        return response is null
            ? TypedResults.NoContent()
            : TypedResults.Ok(response);
    }

    /// <summary>
    /// 为当前登录用户幂等提交一个会话项的 Remembered 或 Forgotten 结果。
    /// </summary>
    public static async Task<Ok<WordStudySessionResponse>> SubmitResultAsync(
        Guid sessionId,
        Guid itemId,
        SubmitWordStudyResultRequest request,
        ClaimsPrincipal principal,
        IWordStudyService studyService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await studyService.SubmitResultAsync(
            EndpointIdentity.GetUserId(principal),
            sessionId,
            itemId,
            request,
            cancellationToken));

    /// <summary>
    /// 以当前登录用户身份幂等放弃仍未完成的指定会话。
    /// </summary>
    public static async Task<Ok<WordStudySessionResponse>> AbandonSessionAsync(
        Guid sessionId,
        ClaimsPrincipal principal,
        IWordStudyService studyService,
        CancellationToken cancellationToken)
        => TypedResults.Ok(await studyService.AbandonSessionAsync(
            EndpointIdentity.GetUserId(principal),
            sessionId,
            cancellationToken));
}
