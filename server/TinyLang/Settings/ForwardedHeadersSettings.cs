namespace TinyLang.Settings;

public sealed record ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeadersSettings";

    public bool Enabled { get; init; }

    public string[] KnownProxies { get; init; } = [];
}
