using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace TinyLang.Endpoints;

/// <summary>
/// 提供 bonus scene 模块的占位和连通性 endpoint。
/// </summary>
public static class BonusScenesEndpoints
{
    /// <summary>
    /// 将 bonus scene 路由注册到 API 路由组。
    /// </summary>
    /// <param name="endpoints">应用顶层 API 路由组。</param>
    /// <returns>完成注册后的同一路由组。</returns>
    public static RouteGroupBuilder MapBonusScenesApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bonus-scenes");

        group.MapGet("/ping", PingAsync);

        return endpoints;
    }

    /// <summary>
    /// 返回包含当前 UTC 时间的连通性响应。
    /// </summary>
    /// <returns>包含 pong 消息和时间戳的成功响应。</returns>
    public static Task<Ok<object>> PingAsync()
    {
        object response = new
        {
            Messages = "pong!",
            Datetime = DateTimeOffset.UtcNow,
        };
        return Task.FromResult(TypedResults.Ok(response));
    }
}
