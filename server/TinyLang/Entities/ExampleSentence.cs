using TinyLang.Entities.Common;

namespace TinyLang.Entities;

/// <summary>
/// 表示隶属于特定词条释义的有序双语例句。
/// </summary>
public sealed class ExampleSentence : BaseEntity
{
    /// <summary>
    /// 创建具有独立标识的例句。
    /// </summary>
    public ExampleSentence()
    {
        Id = Guid.NewGuid();
    }

    public Guid WordSenseId { get; set; }
    public WordSense? WordSense { get; set; }
    public Guid? AudioResourceId { get; set; }
    public AudioResource? AudioResource { get; set; }
    public required string Sentence { get; set; }
    public required string Translation { get; set; }
    public int SortOrder { get; set; }
}
