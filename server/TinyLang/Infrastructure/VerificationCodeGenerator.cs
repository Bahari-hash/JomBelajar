using System.Security.Cryptography;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

/// <summary>
/// 使用密码学安全随机数生成验证码。
/// </summary>
public sealed class VerificationCodeGenerator : IVerificationCodeGenerator
{
    private const string NumericChars = "0123456789";
    private const string AlphanumericChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    /// <inheritdoc />
    public string GenerateNumeric(int length)
    {
        return RandomNumberGenerator.GetString(NumericChars, length);
    }

    /// <inheritdoc />
    public string GenerateAlphanumeric(int length)
    {
        return RandomNumberGenerator.GetString(AlphanumericChars, length);
    }
}
