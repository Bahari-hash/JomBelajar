using TinyLang.Entities.Enums;

namespace TinyLang.Dtos;

public sealed record AvatarPresignRequest
{
    public required string OriginalName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
}

public sealed record EditorMediaPresignRequest
{
    public required string OriginalName { get; init; }
    public required string Extension { get; init; }
    public required string ContentType { get; init; }
    public long Size { get; init; }
    public ResourceModule Module { get; init; }
}

public sealed record PresignResponse(
    Guid ResourceId,
    string PresignedUrl,
    string ObjectName);

public sealed record MediaResourceResponse(
    Guid Id,
    Guid UploaderId,
    string ObjectName,
    string OriginalName,
    ResourceModule Module,
    ResourceStatus Status,
    long Size,
    string Extension,
    string ContentType,
    string? Url,
    DateTimeOffset CreatedAt);
