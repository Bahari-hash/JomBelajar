namespace TinyLang.Models;

public sealed record MediaResourcePresignResult(
    Guid ResourceId,
    string PresignedUrl,
    string ObjectName);
