using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示词条的有序发音文本元数据。
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
    public string? AccentTag { get; set; }
    public string? Ipa { get; set; }
    public bool IsDefault { get; set; }
    public int SortOrder { get; set; }
}
