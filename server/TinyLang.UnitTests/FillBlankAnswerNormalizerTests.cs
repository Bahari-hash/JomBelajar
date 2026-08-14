using FluentAssertions;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证填空题比较键的 Unicode、空白和大小写规范化规则。
/// </summary>
public sealed class FillBlankAnswerNormalizerTests
{
    /// <summary>
    /// 验证 Form KC、首尾清理和连续 Unicode 空白折叠。
    /// </summary>
    [Fact]
    public void NormalizeShouldApplyCompatibilityAndCollapseUnicodeWhitespace()
    {
        var result = FillBlankAnswerNormalizer.Normalize(
            " \u00a0Ｆｏｏ\t\r\nbar\u2003 ",
            caseSensitive: true);

        result.Should().Be("Foo bar");
    }

    /// <summary>
    /// 验证不区分大小写时使用 invariant comparison key。
    /// </summary>
    [Fact]
    public void NormalizeShouldUseInvariantCaseKeyWhenInsensitive()
    {
        FillBlankAnswerNormalizer.Normalize("Straße", caseSensitive: false)
            .Should().Be("STRAßE");
    }

    /// <summary>
    /// 验证区分大小写时保留文本大小写。
    /// </summary>
    [Fact]
    public void NormalizeShouldPreserveCaseWhenSensitive()
    {
        FillBlankAnswerNormalizer.Normalize("Answer", caseSensitive: true)
            .Should().Be("Answer");
    }

    /// <summary>
    /// 验证规范化不会移除标点或组合重音符号。
    /// </summary>
    [Fact]
    public void NormalizeShouldPreservePunctuationAndDiacritics()
    {
        FillBlankAnswerNormalizer.Normalize("café!", caseSensitive: true)
            .Should().Be("café!");
        FillBlankAnswerNormalizer.Normalize("cafe", caseSensitive: true)
            .Should().NotBe("café!");
    }

    /// <summary>
    /// 验证纯空白输入规范化为空比较键。
    /// </summary>
    [Fact]
    public void NormalizeShouldReturnEmptyForWhitespaceOnlyInput()
    {
        FillBlankAnswerNormalizer.Normalize("\t\u2003\r\n", caseSensitive: false)
            .Should().BeEmpty();
    }
}
