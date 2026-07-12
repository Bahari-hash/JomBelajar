namespace TinyLang.Models;

public sealed record EmailMessageWrapper
{
    public required Guid Id { get; init; }
    public required EmailMessage Message { get; init; }
    public required DateTimeOffset CreateTime { get; init; }
}
