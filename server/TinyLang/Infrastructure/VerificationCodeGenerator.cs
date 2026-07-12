using System.Security.Cryptography;
using TinyLang.Interfaces;

namespace TinyLang.Infrastructure;

public sealed class VerificationCodeGenerator : IVerificationCodeGenerator
{
    private const string NumericChars = "0123456789";
    private const string AlphanumericChars = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";

    public string GenerateNumeric(int length)
    {
        return RandomNumberGenerator.GetString(NumericChars, length);
    }

    public string GenerateAlphanumeric(int length)
    {
        return RandomNumberGenerator.GetString(AlphanumericChars, length);
    }
}
