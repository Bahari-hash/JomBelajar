namespace TinyLang.Interfaces;

public interface ISecretHasher
{
    string Hash(string secretPlain);

    bool Verify(string secretPlain, string secretHash);
}
