namespace TinyLang.Models;

public sealed record EmailAddress
{
    public required string Address { get; init; }
    public string? Name { get; init; }
}
