using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示词条与一个已发布发音音频之间的有序关联。
/// </summary>
public sealed class WordPronunciation : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的词条发音关联。
    /// </summary>
    public WordPronunciation()
    {
        Id = Guid.NewGuid();
    }

    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public Guid AudioClipId { get; set; }
    public AudioClip? AudioClip { get; set; }
    public string? AccentTag { get; set; }
    public string? Ipa { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
