using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class SecurityEndpoints
{
    public static RouteGroupBuilder MapSecurityApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users");

        group.MapPut("/me/password", async (
            ResetPasswordRequest request,
            ClaimsPrincipal principal,
            IAccountSecurityService securityService,
            CancellationToken cancellationToken) =>
        {
            await securityService.ResetPasswordAsync(
                EndpointIdentity.GetUserId(principal), request, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPut("/me/email", async (
            ChangeEmailRequest request,
            ClaimsPrincipal principal,
            IAccountSecurityService securityService,
            CancellationToken cancellationToken) =>
        {
            var response = await securityService.ChangeEmailAsync(
                EndpointIdentity.GetUserId(principal), request, cancellationToken);
            return Results.Ok(response);
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapDelete("/delete-me", async (
            [FromBody] DeleteAccountRequest request,
            ClaimsPrincipal principal,
            IAccountSecurityService securityService,
            CancellationToken cancellationToken) =>
        {
            await securityService.DeleteAccountAsync(
                EndpointIdentity.GetUserId(principal), request, cancellationToken);
            return Results.NoContent();
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        return endpoints;
    }
}
