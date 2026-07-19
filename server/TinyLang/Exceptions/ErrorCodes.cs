using System.ComponentModel;

namespace TinyLang.Exceptions;

public enum ErrorCodes
{
    // --** System Errors **--

    [Description("未知系统错误，请联系管理员或稍后再试")]
    UnexpectedError,

    [Description("请求负载参数校验错误")]
    RequestValidationFailed,

    // --** User Errors **--

    [Description("用户名不能为空.")]
    UsernameRequired,

    [Description("用户名长度最大不超过30个字符.")]
    UsernameLengthLimit,

    [Description("用户名只能由字母, 数字, 下划线, 分隔符构成.")]
    UsernameFormatInvalid,

    [Description("邮箱不能为空.")]
    EmailRequired,

    [Description("邮箱长度最大不超过100个字符.")]
    EmailLengthLimit,

    [Description("无效的邮箱格式.")]
    EmailFormatInvalid,

    [Description("密码不能为空.")]
    PasswordRequired,

    [Description("密码长度最大不超过50个字符.")]
    PasswordLengthLimit,

    [Description("密码只能由字母, 数字, 下划线, @#$等符号组成.")]
    PasswordFormatInvalid,

    [Description("验证码不能为空.")]
    VerificationCodeRequired,

    [Description("验证码长度只能是6个字符.")]
    VerificationCodeLengthLimit,

    [Description("验证码只能是数字.")]
    VerificationCodeFormatInvalid,

    [Description("验证码错误或者已经失效.")]
    VerificationCodeInvalid,

    [Description("邮箱已经被占用.")]
    EmailAlreadyExists,

    [Description("用户名已经被占用.")]
    UsernameAlreadyExists,

    [Description("用户不存在.")]
    UserNotFound,

    [Description("用户名或密码错误.")]
    UsernameOrPasswordWrong,

    [Description("用户权鉴无效.")]
    TokenInvalid,

    [Description("用户被封禁, 无法执行操作.")]
    UserAlreadyBanned,

    [Description("用户封禁原因不能为空.")]
    BanUserReasonRequired,

    [Description("用户封禁原因最大不能超过500个字符.")]
    BanUserReasonLengthLimit,

    [Description("用户昵称最大长度不能超过60个字符.")]
    NicknameLengthLimit,

    [Description("用户简介最大长度不超过500个字符.")]
    BioLengthLimit,
}
