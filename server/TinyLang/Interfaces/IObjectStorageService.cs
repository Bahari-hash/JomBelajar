namespace TinyLang.Interfaces;

using TinyLang.Models;

public interface IObjectStorageService
{
    Task<string> PresignPutObjectAsync(
        string objectName,
        string contentType,
        long size,
        CancellationToken cancellationToken = default);

    Task<ObjectStorageMetadata?> GetObjectMetadataAsync(
        string objectName,
        CancellationToken cancellationToken = default);

    Task MoveObjectAsync(
        string sourceObjectName,
        string destinationObjectName,
        CancellationToken cancellationToken = default);

    string GetPublicUrl(string objectName);
}
