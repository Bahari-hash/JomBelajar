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

    [Fact]
    public void QuestionValidatorShouldAcceptDictationWithOrderedBlanksAndAudio()
    {
        var question = new PaperQuestionInput
        {
            Type = PaperQuestionType.Dictation,
            Prompt = "Listen and fill",
            Points = 5,
            SortOrder = 0,
            AudioResourceId = Guid.NewGuid(),
            DictationBlanks =
            [
                new PaperDictationBlankInput { Answer = "hello", SortOrder = 0 },
                new PaperDictationBlankInput { Answer = "world", SortOrder = 1 }
            ]
        };

        new PaperQuestionInputValidator().Validate(question).IsValid.Should().BeTrue();
    }

    [Fact]
    public void QuestionValidatorShouldRejectDictationWithoutAudioOrBlanks()
    {
        var question = new PaperQuestionInput
        {
            Type = PaperQuestionType.Dictation,
            Prompt = "Listen and fill",
            Points = 5,
            SortOrder = 0
        };

        new PaperQuestionInputValidator().Validate(question).IsValid.Should().BeFalse();
    }

    [Fact]
    public void SaveAnswerValidatorShouldAcceptDictationTextAnswers()
    {
        var request = new SavePaperAttemptAnswerRequest
        {
            TextAnswers = [" Hello ", "world"]
        };

        new SavePaperAttemptAnswerRequestValidator().Validate(request).IsValid.Should().BeTrue();
    }

    [Fact]
    public void CreateValidatorShouldRejectNullNestedItemsWithoutThrowing()
    {
        var request = CreateCompleteRequest() with
        {
            Questions =
            [
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.SingleChoice,
                    Prompt = "Choose one",
                    Points = 1,
                    SortOrder = 0,
                    Options = [null!]
                }
            ]
        };

        var result = new CreatePaperRequestValidator().Validate(request);

        result.ShouldContain(ErrorCodes.PaperQuestionCollectionInvalid);
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

    [Fact]
    public void ListValidatorsShouldAcceptOptionalCategoryFilter()
    {
        new AdminPaperListRequestValidator().Validate(new AdminPaperListRequest
        {
            CategoryId = Guid.NewGuid()
        }).IsValid.Should().BeTrue();
        new PaperCatalogRequestValidator().Validate(new PaperCatalogRequest
        {
            CategoryId = Guid.NewGuid()
        }).IsValid.Should().BeTrue();
    }
    /// <summary>
    /// 创建包含单选、判断和填空题的有效请求。
    /// </summary>
    private static CreatePaperRequest CreateCompleteRequest()
        => new()
        {
            Title = "Language Quiz",
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
