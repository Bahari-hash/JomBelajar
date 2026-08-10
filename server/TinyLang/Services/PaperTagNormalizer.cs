namespace TinyLang.Services;

/// <summary>
/// 将试卷标签转换为稳定的持久化形式。
/// </summary>
public static class PaperTagNormalizer
{
    /// <summary>
    /// 裁剪并小写化标签，按首次出现顺序移除空白值和重复值。
    /// </summary>
    public static string[] Normalize(IEnumerable<string> tags)
    {
        ArgumentNullException.ThrowIfNull(tags);

        var normalizedTags = new List<string>();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var tag in tags)
        {
            var normalizedTag = tag.Trim().ToLowerInvariant();
            if (normalizedTag.Length > 0 && seen.Add(normalizedTag))
            {
                normalizedTags.Add(normalizedTag);
            }
        }

        return [.. normalizedTags];
    }
}
