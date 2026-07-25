namespace TinyLang.Enums;

/// <summary>
/// 标识验证码所授权的账户操作。
/// </summary>
public enum VerificationCodePurpose
{
    Register,
    Login,
    ChangeEmail,
    ResetPassword,
    DeleteAccount,
}
