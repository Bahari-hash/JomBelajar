using FluentAssertions;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证试卷标签进入持久化边界前的稳定规范化行为。
/// </summary>
public sealed class PaperTagNormalizerTests
{
    /// <summary>
    /// 验证标签按首次出现顺序完成裁剪、小写转换和不区分大小写去重。
    /// </summary>
    [Fact]
    public void NormalizeShouldCanonicalizeAndPreserveFirstOccurrenceOrder()
    {
        string[] tags = [" Grammar ", "A2", "grammar", "   "];

        var result = PaperTagNormalizer.Normalize(tags);

        result.Should().Equal("grammar", "a2");
    }

    /// <summary>
    /// 验证空标签集合返回独立的空数组。
    /// </summary>
    [Fact]
    public void NormalizeShouldReturnEmptyArrayForEmptyInput()
    {
        var result = PaperTagNormalizer.Normalize([]);

        result.Should().BeEmpty();
        result.Should().BeOfType<string[]>();
    }

    /// <summary>
    /// 验证规范化不会修改调用方提供的标签集合。
    /// </summary>
    [Fact]
    public void NormalizeShouldNotMutateSourceCollection()
    {
        string[] tags = [" Grammar ", "A2"];

        _ = PaperTagNormalizer.Normalize(tags);

        tags.Should().Equal(" Grammar ", "A2");
    }
}
