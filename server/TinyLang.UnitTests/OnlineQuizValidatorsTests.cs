using System.Linq;
using FluentAssertions;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证在线试卷写入、列表和逐题答案请求的有界规则。
/// </summary>
public sealed class OnlineQuizValidatorsTests
{
    /// <summary>
    /// 验证没有题目且及格分为零的草稿可以创建。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldAllowIncompleteDraft()
    {
        var request = new CreatePaperRequest
        {
            Title = "Draft",
            LanguageTag = "en",
            PassingScore = 0
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
    /// 验证及格分不能超过服务端从题目计算的总分。
    /// </summary>
    [Fact]
    public void CreateValidatorShouldRejectPassingScoreAboveTotal()
    {
        var request = CreateCompleteRequest() with { PassingScore = 100 };

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
    /// 创建包含单选、判断和填空题的有效请求。
    /// </summary>
    private static CreatePaperRequest CreateCompleteRequest()
        => new()
        {
            Title = "Language Quiz",
            LanguageTag = "en",
            PassingScore = 5,
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
