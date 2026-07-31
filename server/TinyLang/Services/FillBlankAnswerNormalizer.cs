using System.Text;

namespace TinyLang.Services;

/// <summary>
/// 为填空题标准答案和用户答案生成一致的 Unicode 比较键。
/// </summary>
public static class FillBlankAnswerNormalizer
{
    /// <summary>
    /// 规范化填空文本并按题目大小写策略生成 ordinal 比较键。
    /// </summary>
    /// <param name="value">管理员或用户提交的原始文本。</param>
    /// <param name="caseSensitive">是否保留原始大小写参与比较。</param>
    /// <returns>完成兼容规范化和空白折叠的比较键。</returns>
    public static string Normalize(string value, bool caseSensitive)
    {
        var collapsed = CollapseWhitespace(value.Normalize(NormalizationForm.FormKC));
        return caseSensitive ? collapsed : collapsed.ToUpperInvariant();
    }

    /// <summary>
    /// 生成保留大小写、适合展示和持久化的规范化文本。
    /// </summary>
    /// <param name="value">管理员输入的标准答案文本。</param>
    /// <returns>完成 Form KC 和 Unicode 空白折叠的展示文本。</returns>
    public static string NormalizeForDisplay(string value)
        => CollapseWhitespace(value.Normalize(NormalizationForm.FormKC));

    /// <summary>
    /// 折叠连续 Unicode 空白并移除首尾空白。
    /// </summary>
    private static string CollapseWhitespace(string value)
    {
        var builder = new StringBuilder(value.Length);
        var pendingSpace = false;
        foreach (var rune in value.EnumerateRunes())
        {
            if (Rune.IsWhiteSpace(rune))
            {
                pendingSpace = builder.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                builder.Append(' ');
                pendingSpace = false;
            }
            builder.Append(rune.ToString());
        }

        return builder.ToString();
    }
}
