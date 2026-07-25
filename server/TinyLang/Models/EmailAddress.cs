namespace TinyLang.Models;

/// <summary>
/// 表示邮件收件人或发件人的地址及可选显示名称。
/// </summary>
public sealed record EmailAddress
{
    public required string Address { get; init; }
    public string? Name { get; init; }
}
