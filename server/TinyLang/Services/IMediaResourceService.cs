using TinyLang.Entities.Enums;
using TinyLang.Entities;
using TinyLang.Models;

namespace TinyLang.Services;

public interface IMediaResourceService
{
    Task<MediaResourcePresignResult> CreatePendingResourceAndPresignAsync(
        Guid uploaderId,
        string originalName,
        string extension,
        long size,
        string contentType,
        ResourceModule module,
        CancellationToken cancellationToken = default);

    Task<MediaResource> ConfirmAsync(
        Guid resourceId,
        Guid uploaderId,
        CancellationToken cancellationToken = default);
}
