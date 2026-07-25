namespace TinyLang.Interfaces;

/// <summary>
/// 定义生成随机数字或字母数字验证码的契约。
/// </summary>
public interface IVerificationCodeGenerator
{
    /// <summary>
    /// 生成指定长度的随机数字验证码。
    /// </summary>
    /// <param name="length">验证码字符数。</param>
    /// <returns>仅包含数字的验证码。</returns>
    string GenerateNumeric(int length);

    /// <summary>
    /// 生成指定长度的随机字母数字验证码。
    /// </summary>
    /// <param name="length">验证码字符数。</param>
    /// <returns>包含字母和数字的验证码。</returns>
    string GenerateAlphanumeric(int length);
}
