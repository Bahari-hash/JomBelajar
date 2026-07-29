using FluentAssertions;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条 Unicode、大小写比较键和语言标签兼容规则。
/// </summary>
public sealed class WordTextNormalizerTests
{
    /// <summary>
    /// 验证展示词头保留大小写并统一组合字符。
    /// </summary>
    [Fact]
    public void DisplayHeadwordShouldTrimAndUseCanonicalComposition()
    {
        var result = WordTextNormalizer.NormalizeHeadwordForDisplay("  Cafe\u0301  ");

        result.Should().Be("Caf\u00e9");
    }

    /// <summary>
    /// 验证比较键统一兼容字符、首尾空白和大小写。
    /// </summary>
    [Fact]
    public void ComparisonKeyShouldNormalizeUnicodeWhitespaceAndCase()
    {
        var first = WordTextNormalizer.CreateHeadwordComparisonKey("  Hello  ");
        var second = WordTextNormalizer.CreateHeadwordComparisonKey("hello");
        var compatibility = WordTextNormalizer.CreateHeadwordComparisonKey("\uff28\uff45\uff4c\uff4c\uff4f");

        first.Should().Be("HELLO");
        second.Should().Be(first);
        compatibility.Should().Be(first);
    }

    /// <summary>
    /// 验证相同标签和父子 subtag 兼容，而并列地区标签不兼容。
    /// </summary>
    [Theory]
    [InlineData("en", "en-US", true)]
    [InlineData("EN-us", "en-US", true)]
    [InlineData("en-US", "en-GB", false)]
    [InlineData("zh-Hans", "zh-Hant", false)]
    [InlineData("fr", "en", false)]
    public void LanguageCompatibilityShouldUseSubtagBoundaries(
        string left,
        string right,
        bool expected)
    {
        WordTextNormalizer.AreLanguageTagsCompatible(left, right)
            .Should().Be(expected);
    }
}
