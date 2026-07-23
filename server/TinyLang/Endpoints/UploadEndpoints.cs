using System.Security.Claims;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TinyLang.Constants;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Services;

namespace TinyLang.Endpoints;

public static class UploadEndpoints
{
    public static RouteGroupBuilder MapUploadsApi(this RouteGroupBuilder endpoints)
    {
        var group = endpoints.MapGroup("/uploads");

        group.MapPost("/users/avatar/presign", async (
            AvatarPresignRequest request,
            ClaimsPrincipal principal,
            IMediaResourceService mediaResourceService,
            CancellationToken cancellationToken) =>
        {
            var response = await mediaResourceService.CreatePendingResourceAndPresignAsync(
                EndpointIdentity.GetUserId(principal),
                request.OriginalName,
                request.Extension,
                request.Size,
                request.ContentType,
                ResourceModule.Avatar,
                cancellationToken);
            return Results.Ok(new PresignResponse(
                response.ResourceId,
                response.PresignedUrl,
                response.ObjectName));
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        group.MapPost("/editor/media/presign", async (
            EditorMediaPresignRequest request,
            ClaimsPrincipal principal,
            IMediaResourceService mediaResourceService,
            CancellationToken cancellationToken) =>
        {
            var response = await mediaResourceService.CreatePendingResourceAndPresignAsync(
                EndpointIdentity.GetUserId(principal),
                request.OriginalName,
                request.Extension,
                request.Size,
                request.ContentType,
                request.Module,
                cancellationToken);
            return Results.Ok(new PresignResponse(
                response.ResourceId,
                response.PresignedUrl,
                response.ObjectName));
        }).RequireAuthorization(AuthorizationPolicies.RequireEditor);

        group.MapPut("/resources/{id:guid}/confirm", async (
            Guid id,
            ClaimsPrincipal principal,
            IMediaResourceService mediaResourceService,
            CancellationToken cancellationToken) =>
        {
            var resource = await mediaResourceService.ConfirmAsync(
                id,
                EndpointIdentity.GetUserId(principal),
                cancellationToken);
            return Results.Ok(ToResponse(resource));
        }).RequireAuthorization(AuthorizationPolicies.RequireUser);

        return endpoints;
    }

    private static MediaResourceResponse ToResponse(MediaResource resource)
        => new(
            resource.Id,
            resource.UploaderId,
            resource.ObjectName,
            resource.OriginalName,
            resource.Module,
            resource.Status,
            resource.Size,
            resource.Extension,
            resource.ContentType,
            resource.Url,
            resource.CreatedAt);
}
