namespace TinyLang.Settings;

/// <summary>
/// 描述反向代理转发头的启用状态和可信代理地址。
/// </summary>
public sealed record ForwardedHeadersSettings
{
    public const string SectionName = "ForwardedHeadersSettings";

    public bool Enabled { get; init; }

    public string[] KnownProxies { get; init; } = [];

    public string[] KnownNetworks { get; init; } = [];
}
