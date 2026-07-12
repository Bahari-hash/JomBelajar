namespace TinyLang.Interfaces;

public interface IVerificationCodeGenerator
{
    string GenerateNumeric(int length);

    string GenerateAlphanumeric(int length);
}
