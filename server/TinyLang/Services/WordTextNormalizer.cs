using System.Text;

namespace TinyLang.Services;

/// <summary>
/// 集中提供词条文本、词头比较键和语言标签的确定性规范化规则。
/// </summary>
public static class WordTextNormalizer
{
    /// <summary>
    /// 规范化用于展示的词头，同时保留其大小写形式。
    /// </summary>
    /// <param name="value">客户端提交的词头。</param>
    /// <returns>去除首尾空白并采用 Unicode Form C 的词头。</returns>
    public static string NormalizeHeadwordForDisplay(string value)
        => value.Trim().Normalize(NormalizationForm.FormC);

    /// <summary>
    /// 创建仅用于查重和筛选的大小写无关词头比较键。
    /// </summary>
    /// <param name="value">客户端提交或已经规范化的词头。</param>
    /// <returns>采用 Unicode Form KC 和 invariant uppercase 的比较键。</returns>
    public static string CreateHeadwordComparisonKey(string value)
        => value.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();

    /// <summary>
    /// 规范化必填纯文本。
    /// </summary>
    /// <param name="value">必填文本。</param>
    /// <returns>去除首尾空白后的文本。</returns>
    public static string NormalizeRequiredText(string value) => value.Trim();

    /// <summary>
    /// 规范化可选纯文本，并将空白转换为 null。
    /// </summary>
    /// <param name="value">可选文本。</param>
    /// <returns>规范化后的文本或 null。</returns>
    public static string? NormalizeOptionalText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

}
