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

public static class SecurityEndpoints
{
    public static RouteGroupBuilder MapSecurityApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/users")
            .RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPut("/me/reset-password", ResetPasswordAsync);

        group.MapPut("/me/change-email", ChangeEmailAsync);

        group.MapDelete("/me/delete-account", DeleteAccountAsync);

        return endpoints;
    }

    public static async Task<NoContent> ResetPasswordAsync(
        ResetPasswordRequest request,
        ClaimsPrincipal principal,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        await securityService.ResetPasswordAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.NoContent();
    }

    public static async Task<Ok<ChangeEmailResponse>> ChangeEmailAsync(
        ChangeEmailRequest request,
        ClaimsPrincipal principal,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        var response = await securityService.ChangeEmailAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.Ok(response);
    }

    public static async Task<NoContent> DeleteAccountAsync(
        [FromBody] DeleteAccountRequest request,
        ClaimsPrincipal principal,
        IAccountSecurityService securityService,
        CancellationToken cancellationToken)
    {
        await securityService.DeleteAccountAsync(
            EndpointIdentity.GetUserId(principal), request, cancellationToken);
        return TypedResults.NoContent();
    }
}
