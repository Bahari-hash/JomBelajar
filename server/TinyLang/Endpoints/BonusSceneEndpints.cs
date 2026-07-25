using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;

namespace TinyLang.Endpoints;

public static class BonusScenesEndpoints
{
    public static RouteGroupBuilder MapBonusScenesApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bonus-scenes");

        group.MapGet("/ping", PingAsync);

        return endpoints;
    }

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
