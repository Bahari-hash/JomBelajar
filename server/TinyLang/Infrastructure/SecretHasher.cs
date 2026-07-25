using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用 BCrypt enhanced 模式散列和验证敏感明文。
/// </summary>
public sealed class SecretHasher : ISecretHasher
{
    /// <inheritdoc />
    public string Hash(string secretPlain)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(secretPlain);
    }

    /// <inheritdoc />
    public bool Verify(string secretPlain, string secretHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(secretPlain, secretHash);
    }
}
