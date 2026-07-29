using TinyLang.Entities.Common;
using TinyLang.Entities.Enums;

namespace TinyLang.Entities;

/// <summary>
/// 表示词条中的一个有序释义及其例句集合。
/// </summary>
public sealed class WordSense : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的词条释义。
    /// </summary>
    public WordSense()
    {
        Id = Guid.NewGuid();
    }

    public Guid WordId { get; set; }
    public Word? Word { get; set; }
    public PartOfSpeech PartOfSpeech { get; set; }
    public required string Definition { get; set; }
    public required string DefinitionLanguageTag { get; set; }
    public string? UsageNote { get; set; }
    public int SortOrder { get; set; }
    public ICollection<ExampleSentence> Examples { get; set; } = [];
}
