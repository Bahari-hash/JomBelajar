using System.Text.RegularExpressions;

namespace TinyLang.Dtos;

/// <summary>
/// 提供媒体请求共享的源生成验证表达式。
/// </summary>
internal static partial class MediaValidationPatterns
{
    /// <summary>
    /// 匹配媒体模块基础验证使用的 BCP 47 风格语言标签。
    /// </summary>
    [GeneratedRegex("^[A-Za-z]{2,8}(?:-[A-Za-z0-9]{1,8})*$", RegexOptions.CultureInvariant)]
    public static partial Regex LanguageTag();
}
