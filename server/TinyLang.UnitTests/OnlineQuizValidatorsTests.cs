using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证在线试卷写入、列表和逐题答案请求的有界规则。
/// </summary>
public sealed class OnlineQuizValidatorsTests
{
    /// <summary>
    /// 验证没有题目且使用默认及格百分比的草稿可以创建。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldAllowIncompleteDraft()
    {
        var request = new CreatePaperRequest
        {
            Title = "Draft",
            LanguageTag = "en",
            PassingScorePercentage = 60
        };

        new CreatePaperRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证三类题目的有效完整请求通过校验。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldAcceptThreeQuestionTypes()
    {
        var request = CreateCompleteRequest();

        new CreatePaperRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证试卷标签数量不能超过写入边界。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldRejectTooManyTags()
    {
        var request = CreateCompleteRequest() with
        {
            Tags = Enumerable.Range(0, OnlineQuizConstraints.MaxPaperTagCount + 1)
                .Select(value => $"tag-{value}").ToArray()
        };

        new CreatePaperRequestValidator().Validate(request)
            .ShouldContain(ErrorCodes.PaperTagCountLimit);
    }

    /// <summary>
    /// 验证标签长度限制应用于裁剪后的值。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldRejectTrimmedTagAboveLengthLimit()
    {
        var request = CreateCompleteRequest() with
        {
            Tags = [$"  {new string('a', OnlineQuizConstraints.MaxPaperTagLength + 1)}  "]
        };

        new CreatePaperRequestValidator().Validate(request)
            .ShouldContain(ErrorCodes.PaperTagLengthLimit);
    }

    /// <summary>
    /// 验证空白或包含控制字符的标签被拒绝。
    /// </summary>
    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("gram\nmar")]
    public void CreateValidatorShouldRejectInvalidTag(string tag)
    {
        var request = CreateCompleteRequest() with { Tags = [tag] };

        new CreatePaperRequestValidator().Validate(request)
            .ShouldContain(ErrorCodes.PaperTagInvalid);
    }

    /// <summary>
    /// 验证标签在裁剪且忽略大小写后不能重复。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldRejectCaseInsensitiveTagDuplicates()
    {
        var request = CreateCompleteRequest() with
        {
            Tags = ["Grammar", " grammar "]
        };

        new CreatePaperRequestValidator().Validate(request)
            .ShouldContain(ErrorCodes.PaperTagDuplicate);
    }

    /// <summary>
    /// 验证有效的规范标签集合通过校验。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldAcceptValidTags()
    {
        var request = CreateCompleteRequest() with { Tags = ["grammar", "a2"] };

        new CreatePaperRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证跨题型答案字段和多个正确单选项被拒绝。
    /// </summary>
    [Fact]
    public void QuestionValidatorShouldRejectInvalidTypeShape()
    {
        var question = CreateCompleteRequest().Questions.First() with
        {
            CorrectBoolean = true,
            Options =
            [
                new PaperQuestionOptionInput
                {
                    Text = "A",
                    IsCorrect = true,
                    SortOrder = 0
                },
                new PaperQuestionOptionInput
                {
                    Text = "B",
                    IsCorrect = true,
                    SortOrder = 1
                }
            ]
        };

        new PaperQuestionInputValidator().Validate(question).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证填空标准答案在题目比较策略下不能规范化为重复键。
    /// </summary>
    [Fact]
    public void QuestionValidatorShouldRejectNormalizedAnswerDuplicates()
    {
        var question = CreateCompleteRequest().Questions.Last() with
        {
            AcceptedAnswers =
            [
                new FillBlankAcceptedAnswerInput { Text = " New\tYork ", SortOrder = 0 },
                new FillBlankAcceptedAnswerInput { Text = "new york", SortOrder = 1 }
            ]
        };

        new PaperQuestionInputValidator().Validate(question).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证及格分百分比必须位于一到一百之间。
    /// </summary>
    [Theory]
    [InlineData(0)]
    [InlineData(101)]
    public void CreateValidatorShouldRejectInvalidPassingScorePercentage(
        int percentage)
    {
        var request = CreateCompleteRequest() with
        {
            PassingScorePercentage = percentage
        };

        new CreatePaperRequestValidator().Validate(request).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证逐题保存请求最多只能携带一个答案字段。
    /// </summary>
    [Fact]
    public void SaveAnswerValidatorShouldRejectMixedAnswerFields()
    {
        var request = new SavePaperAttemptAnswerRequest
        {
            SelectedOptionId = Guid.NewGuid(),
            BooleanAnswer = false
        };

        new SavePaperAttemptAnswerRequestValidator()
            .Validate(request).IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证状态动作必须提供非空并发标识。
    /// </summary>
    [Fact]
    public void MutationValidatorShouldRequireConcurrencyStamp()
    {
        var validator = new PaperMutationRequestValidator();

        validator.Validate(new PaperMutationRequest()).IsValid.Should().BeFalse();
        validator.Validate(new PaperMutationRequest
        {
            ConcurrencyStamp = Guid.NewGuid()
        }).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证保存答案不能使用空 PUT 隐式表达清除。
    /// </summary>
    [Fact]
    public void SaveAnswerValidatorShouldRequireExactlyOneAnswer()
    {
        new SavePaperAttemptAnswerRequestValidator()
            .Validate(new SavePaperAttemptAnswerRequest())
            .IsValid.Should().BeFalse();
    }

    /// <summary>
    /// 验证管理员列表可显式筛选归档状态。
    /// </summary>
    [Fact]
    public void AdminListValidatorShouldAcceptArchivedStatus()
    {
        new AdminPaperListRequestValidator().Validate(new AdminPaperListRequest
        {
            Status = PaperPublicationStatus.Archived
        }).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证两个试卷列表均接受未提供、空白和混合大小写的标签筛选。
    /// </summary>
    [Theory]
    [InlineData(null)]
    [InlineData("   ")]
    [InlineData("  GrAmMaR  ")]
    public void ListValidatorsShouldAcceptOptionalTagFilter(string? tag)
    {
        new AdminPaperListRequestValidator().Validate(new AdminPaperListRequest
        {
            Tag = tag
        }).IsValid.Should().BeTrue();
        new PaperCatalogRequestValidator().Validate(new PaperCatalogRequest
        {
            Tag = tag
        }).IsValid.Should().BeTrue();
    }

    /// <summary>
    /// 验证两个试卷列表均拒绝超过原始长度边界的标签筛选。
    /// </summary>
    [Fact]
    public void ListValidatorsShouldRejectTagFilterAboveLengthLimit()
    {
        var tag = new string('a', OnlineQuizConstraints.MaxPaperTagLength + 1);

        new AdminPaperListRequestValidator().Validate(new AdminPaperListRequest
        {
            Tag = tag
        }).ShouldContain(ErrorCodes.PaperTagLengthLimit);
        new PaperCatalogRequestValidator().Validate(new PaperCatalogRequest
        {
            Tag = tag
        }).ShouldContain(ErrorCodes.PaperTagLengthLimit);
    }

    /// <summary>
    /// 验证两个试卷列表均拒绝包含控制字符的标签筛选。
    /// </summary>
    [Fact]
    public void ListValidatorsShouldRejectTagFilterWithControlCharacters()
    {
        const string tag = "gram\nmar";

        new AdminPaperListRequestValidator().Validate(new AdminPaperListRequest
        {
            Tag = tag
        }).ShouldContain(ErrorCodes.PaperTagInvalid);
        new PaperCatalogRequestValidator().Validate(new PaperCatalogRequest
        {
            Tag = tag
        }).ShouldContain(ErrorCodes.PaperTagInvalid);
    }

    /// <summary>
    /// 验证标签目录分页和关键词使用与其他目录一致的有界规则。
    /// </summary>
    [Fact]
    public void PaperTagListValidatorShouldBoundPagingAndKeyword()
    {
        var validator = new PaperTagListRequestValidator();

        validator.Validate(new PaperTagListRequest()).IsValid.Should().BeTrue();
        validator.Validate(new PaperTagListRequest { Page = 0 })
            .ShouldContain(ErrorCodes.PageInvalid);
        validator.Validate(new PaperTagListRequest { PageSize = 101 })
            .ShouldContain(ErrorCodes.PageSizeInvalid);
        validator.Validate(new PaperTagListRequest { Keyword = new string('a', 201) })
            .ShouldContain(ErrorCodes.KeywordLengthLimit);
        validator.Validate(new PaperTagListRequest { Keyword = "bad\nkeyword" })
            .ShouldContain(ErrorCodes.KeywordInvalid);
    }
    /// <summary>
    /// 创建包含单选、判断和填空题的有效请求。
    /// </summary>
    private static CreatePaperRequest CreateCompleteRequest()
        => new()
        {
            Title = "Language Quiz",
            LanguageTag = "en",
            PassingScorePercentage = 60,
            Questions =
            [
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.SingleChoice,
                    Prompt = "Choose one",
                    Points = 2,
                    SortOrder = 0,
                    Options =
                    [
                        new PaperQuestionOptionInput
                        {
                            Text = "A",
                            IsCorrect = true,
                            SortOrder = 0
                        },
                        new PaperQuestionOptionInput { Text = "B", SortOrder = 1 }
                    ]
                },
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.TrueFalse,
                    Prompt = "True?",
                    Points = 2,
                    SortOrder = 1,
                    CorrectBoolean = true
                },
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.FillBlank,
                    Prompt = "Fill",
                    Points = 3,
                    SortOrder = 2,
                    AcceptedAnswers =
                    [
                        new FillBlankAcceptedAnswerInput
                        {
                            Text = "answer",
                            SortOrder = 0
                        }
                    ]
                }
            ]
        };
}
