namespace TinyLang.Constants;

/// <summary>
/// 定义 TinyLang 访问令牌中使用的 JWT claim 名称。
/// </summary>
public static class JwtClaimNamesExtension
{
    public const string UserId = "uid";
    public const string TokenId = "jti";
    public const string Expiration = "exp";
    public const string TokenVersion = "ver";
    public const string Name = "name";
    public const string Role = "role";
}
