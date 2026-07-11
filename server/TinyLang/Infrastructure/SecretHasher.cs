using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class SecretHasher : ISecretHasher
{
    public string Hash(string secretPlain)
    {
        return BCrypt.Net.BCrypt.EnhancedHashPassword(secretPlain);
    }

    public bool Verify(string secretPlain, string secretHash)
    {
        return BCrypt.Net.BCrypt.EnhancedVerify(secretPlain, secretHash);
    }
}
