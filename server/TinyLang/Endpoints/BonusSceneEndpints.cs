using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace TinyLang.Endpoints;

public static class BonusScenesEndpoints
{
    public static RouteGroupBuilder MapBonusScenesApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/bonus-scenes");

        group.MapGet("/ping", () =>
        {
            var someResponseFromServer = new
            {
                Messages = "pong!",
                Datetime = DateTimeOffset.UtcNow,
            };
            return Results.Ok(someResponseFromServer);
        });

        return endpoints;
    }
}
