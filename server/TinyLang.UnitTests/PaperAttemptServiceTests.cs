using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证试卷测验创建、用户隔离、答案保存、判分和幂等状态转换。
/// </summary>
public sealed class PaperAttemptServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 29, 10, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证同一用户和试卷的活动测验会恢复，提交后编号递增。
    /// </summary>
    [Fact]
    public async Task StartShouldResumeActiveAndIncrementAfterSubmission()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);

        var first = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var resumed = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        await service.SubmitAsync(
            userId,
            first.Attempt.Id,
            TestContext.Current.CancellationToken);
        var second = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);

        first.WasCreated.Should().BeTrue();
        resumed.WasCreated.Should().BeFalse();
        resumed.Attempt.Id.Should().Be(first.Attempt.Id);
        second.WasCreated.Should().BeTrue();
        second.Attempt.AttemptNumber.Should().Be(2);
    }

    /// <summary>
    /// 验证下架阻止新测验但不阻止已有活动测验恢复。
    /// </summary>
    [Fact]
    public async Task UnpublishShouldBlockNewAttemptButAllowActiveResume()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var adminId = Guid.NewGuid();
        var paperService = PaperServiceTests.CreateService(db);
        var draft = await paperService.CreateDraftAsync(
            adminId,
            PaperServiceTests.CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var paper = await paperService.PublishAsync(
            draft.Id,
            adminId,
            PaperServiceTests.Mutation(draft),
            TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var firstUserId = Guid.NewGuid();
        var active = await service.StartAsync(
            firstUserId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var currentPaper = await paperService.GetAdminByIdAsync(
            paper.Id,
            TestContext.Current.CancellationToken);
        await paperService.UnpublishAsync(
            paper.Id,
            adminId,
            PaperServiceTests.Mutation(currentPaper),
            TestContext.Current.CancellationToken);

        var resumed = await service.StartAsync(
            firstUserId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var startOtherUser = async () => await service.StartAsync(
            Guid.NewGuid(),
            paper.Id,
            TestContext.Current.CancellationToken);

        resumed.WasCreated.Should().BeFalse();
        resumed.Attempt.Id.Should().Be(active.Attempt.Id);
        await startOtherUser.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证三类题型保存正确字段并允许在提交前覆盖答案。
    /// </summary>
    [Fact]
    public async Task SaveAnswerShouldValidateShapeAndAllowOverwrite()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var single = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.SingleChoice);
        var firstOption = single.Options.First();
        var secondOption = single.Options.Last();

        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            single.Id,
            new SavePaperAttemptAnswerRequest
            {
                SelectedOptionId = firstOption.Id
            },
            TestContext.Current.CancellationToken);
        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            single.Id,
            new SavePaperAttemptAnswerRequest
            {
                SelectedOptionId = secondOption.Id
            },
            TestContext.Current.CancellationToken);
        var restored = await service.GetAttemptAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        restored.Questions.Single(value => value.Id == single.Id)
            .SavedAnswer!.SelectedOptionId.Should().Be(secondOption.Id);

        var wrongShape = async () => await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            single.Id,
            new SavePaperAttemptAnswerRequest { BooleanAnswer = true },
            TestContext.Current.CancellationToken);
        await wrongShape.Should().ThrowAsync<RequestValidationException>();
    }

    /// <summary>
    /// 验证 OptionId 必须属于当前题目且其他用户不能读取测验。
    /// </summary>
    [Fact]
    public async Task AnswerAndAttemptShouldEnforceQuestionAndUserOwnership()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var single = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.SingleChoice);

        var foreignOption = async () => await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            single.Id,
            new SavePaperAttemptAnswerRequest
            {
                SelectedOptionId = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken);
        var crossUser = async () => await service.GetAttemptAsync(
            Guid.NewGuid(),
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        await foreignOption.Should().ThrowAsync<RequestValidationException>();
        await crossUser.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证提交补齐未答题并按三类题型计算稳定成绩。
    /// </summary>
    [Fact]
    public async Task SubmitShouldScoreAnswersAndIncludeUnansweredResults()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var single = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.SingleChoice);
        var fill = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.FillBlank);
        var correctOption = single.Options.Single(value => value.IsCorrect);

        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            single.Id,
            new SavePaperAttemptAnswerRequest
            {
                SelectedOptionId = correctOption.Id
            },
            TestContext.Current.CancellationToken);
        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            fill.Id,
            new SavePaperAttemptAnswerRequest { TextAnswer = " new\tYORK " },
            TestContext.Current.CancellationToken);

        var result = await service.SubmitAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        result.Score.Should().Be(6);
        result.PaperTotalScore.Should().Be(9);
        result.IsPassed.Should().BeTrue();
        result.Questions.Should().HaveCount(3);
        result.Questions.Single(value => value.QuestionId == single.Id)
            .CorrectOptionId.Should().Be(correctOption.Id);
        result.Questions.Single(value =>
            value.Type == PaperQuestionType.TrueFalse).IsAnswered.Should().BeFalse();
        result.Questions.Single(value =>
            value.Type == PaperQuestionType.TrueFalse).AwardedPoints.Should().Be(0);
        result.Questions.Single(value => value.QuestionId == fill.Id)
            .AcceptedAnswers.Should().Contain("New York");
    }

    /// <summary>
    /// 验证重复提交返回相同时间和结果，提交后不再允许覆盖答案。
    /// </summary>
    [Fact]
    public async Task SubmitShouldBeIdempotentAndPreventLaterAnswerChanges()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);

        var first = await service.SubmitAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);
        var repeated = await service.SubmitAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);
        var save = async () => await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            paper.Questions.First().Id,
            new SavePaperAttemptAnswerRequest
            {
                SelectedOptionId = paper.Questions.First().Options.First().Id
            },
            TestContext.Current.CancellationToken);

        repeated.Score.Should().Be(first.Score);
        repeated.SubmittedAt.Should().Be(first.SubmittedAt);
        repeated.Questions.Should().BeEquivalentTo(first.Questions, options =>
            options.WithStrictOrdering());
        await save.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证结果仅对已经提交且属于当前用户的测验开放。
    /// </summary>
    [Fact]
    public async Task ResultShouldRequireSubmittedOwnerAttempt()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);

        var inProgress = async () => await service.GetResultAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);
        var crossUser = async () => await service.GetResultAsync(
            Guid.NewGuid(),
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        await inProgress.Should().ThrowAsync<ConflictException>();
        await crossUser.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证区分大小写的填空题使用 ordinal 精确判分。
    /// </summary>
    [Fact]
    public async Task SubmitShouldRespectFillBlankCaseSensitivity()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var adminId = Guid.NewGuid();
        var paperService = PaperServiceTests.CreateService(db);
        var request = PaperServiceTests.CreateCompleteRequest();
        var fillInput = request.Questions.Single(value =>
            value.Type == PaperQuestionType.FillBlank) with
        {
            FillBlankCaseSensitive = true,
            AcceptedAnswers =
            [
                new FillBlankAcceptedAnswerInput { Text = "Answer", SortOrder = 0 }
            ]
        };
        request = request with
        {
            Questions = request.Questions.Select(value =>
                value.Type == PaperQuestionType.FillBlank ? fillInput : value).ToArray()
        };
        var draft = await paperService.CreateDraftAsync(
            adminId,
            request,
            TestContext.Current.CancellationToken);
        var paper = await paperService.PublishAsync(
            draft.Id,
            adminId,
            PaperServiceTests.Mutation(draft),
            TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var userId = Guid.NewGuid();
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var fill = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.FillBlank);
        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            fill.Id,
            new SavePaperAttemptAnswerRequest { TextAnswer = "answer" },
            TestContext.Current.CancellationToken);

        var result = await service.SubmitAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        result.Questions.Single(value => value.QuestionId == fill.Id)
            .IsCorrect.Should().BeFalse();
    }

    /// <summary>
    /// 验证本人可以查询下架试卷的历史，其他用户看不到该历史。
    /// </summary>
    [Fact]
    public async Task HistoryShouldRemainAvailableAfterUnpublishOnlyToOwner()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var adminId = Guid.NewGuid();
        var paperService = PaperServiceTests.CreateService(db);
        var draft = await paperService.CreateDraftAsync(
            adminId,
            PaperServiceTests.CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var paper = await paperService.PublishAsync(
            draft.Id,
            adminId,
            PaperServiceTests.Mutation(draft),
            TestContext.Current.CancellationToken);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var currentPaper = await paperService.GetAdminByIdAsync(
            paper.Id,
            TestContext.Current.CancellationToken);
        await paperService.UnpublishAsync(
            paper.Id,
            adminId,
            PaperServiceTests.Mutation(currentPaper),
            TestContext.Current.CancellationToken);

        var history = await service.GetHistoryAsync(
            userId,
            paper.Id,
            new PaperAttemptListRequest(),
            TestContext.Current.CancellationToken);
        var otherUser = async () => await service.GetHistoryAsync(
            Guid.NewGuid(),
            paper.Id,
            new PaperAttemptListRequest(),
            TestContext.Current.CancellationToken);

        history.Items.Should().ContainSingle();
        await otherUser.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证活动测验可以显式、幂等清除答案并保持刷新字段完整。
    /// </summary>
    [Fact]
    public async Task ClearAnswerShouldBeIdempotentAndPreserveSafeAttemptFields()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var question = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.TrueFalse);
        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            question.Id,
            new SavePaperAttemptAnswerRequest { BooleanAnswer = true },
            TestContext.Current.CancellationToken);

        await service.ClearAnswerAsync(
            userId,
            started.Attempt.Id,
            question.Id,
            TestContext.Current.CancellationToken);
        await service.ClearAnswerAsync(
            userId,
            started.Attempt.Id,
            question.Id,
            TestContext.Current.CancellationToken);
        var restored = await service.GetAttemptAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        restored.PaperTotalScore.Should().Be(paper.TotalScore);
        restored.PaperPassingScore.Should().Be(paper.PassingScore);
        restored.Questions.Single(value => value.Id == question.Id).Points
            .Should().Be(question.Points);
        restored.Questions.Single(value => value.Id == question.Id).SavedAnswer
            .Should().BeNull();

        var foreignQuestion = async () => await service.ClearAnswerAsync(
            userId,
            started.Attempt.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        await foreignQuestion.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证提交后不能清除答案且其他用户无法操作该 Attempt。
    /// </summary>
    [Fact]
    public async Task ClearAnswerShouldEnforceStatusAndOwnership()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var paper = await CreatePublishedPaperAsync(db);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var questionId = paper.Questions.First().Id;
        await service.SubmitAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);

        var submitted = async () => await service.ClearAnswerAsync(
            userId,
            started.Attempt.Id,
            questionId,
            TestContext.Current.CancellationToken);
        var otherUser = async () => await service.ClearAnswerAsync(
            Guid.NewGuid(),
            started.Attempt.Id,
            questionId,
            TestContext.Current.CancellationToken);

        (await submitted.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperAttemptNotInProgress);
        await otherUser.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 验证归档阻止新测验但不影响已有测验保存、清除、提交和结果。
    /// </summary>
    [Fact]
    public async Task ArchivedPaperShouldKeepExistingAttemptUsable()
    {
        await using var db = PaperServiceTests.CreateDbContext();
        var adminId = Guid.NewGuid();
        var paperService = PaperServiceTests.CreateService(db);
        var draft = await paperService.CreateDraftAsync(
            adminId,
            PaperServiceTests.CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var paper = await paperService.PublishAsync(
            draft.Id,
            adminId,
            PaperServiceTests.Mutation(draft),
            TestContext.Current.CancellationToken);
        var userId = Guid.NewGuid();
        var service = CreateService(db);
        var started = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        var currentPaper = await paperService.GetAdminByIdAsync(
            paper.Id,
            TestContext.Current.CancellationToken);
        var unpublished = await paperService.UnpublishAsync(
            paper.Id,
            adminId,
            PaperServiceTests.Mutation(currentPaper),
            TestContext.Current.CancellationToken);
        await paperService.ArchiveAsync(
            paper.Id,
            adminId,
            PaperServiceTests.Mutation(unpublished),
            TestContext.Current.CancellationToken);
        var question = paper.Questions.Single(value =>
            value.Type == PaperQuestionType.TrueFalse);

        var resumed = await service.StartAsync(
            userId,
            paper.Id,
            TestContext.Current.CancellationToken);
        await service.SaveAnswerAsync(
            userId,
            started.Attempt.Id,
            question.Id,
            new SavePaperAttemptAnswerRequest { BooleanAnswer = true },
            TestContext.Current.CancellationToken);
        await service.ClearAnswerAsync(
            userId,
            started.Attempt.Id,
            question.Id,
            TestContext.Current.CancellationToken);
        var result = await service.SubmitAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);
        var persisted = await service.GetResultAsync(
            userId,
            started.Attempt.Id,
            TestContext.Current.CancellationToken);
        var newAttempt = async () => await service.StartAsync(
            Guid.NewGuid(),
            paper.Id,
            TestContext.Current.CancellationToken);

        resumed.WasCreated.Should().BeFalse();
        resumed.Attempt.Id.Should().Be(started.Attempt.Id);
        persisted.Should().BeEquivalentTo(result);
        await newAttempt.Should().ThrowAsync<NotFoundException>();
    }

    /// <summary>
    /// 创建并发布包含三类完整题目的试卷。
    /// </summary>
    private static async Task<AdminPaperResponse> CreatePublishedPaperAsync(
        ApplicationDbContext db)
    {
        var paperService = PaperServiceTests.CreateService(db);
        var adminId = Guid.NewGuid();
        var draft = await paperService.CreateDraftAsync(
            adminId,
            PaperServiceTests.CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        return await paperService.PublishAsync(
            draft.Id,
            adminId,
            PaperServiceTests.Mutation(draft),
            TestContext.Current.CancellationToken);
    }

    /// <summary>
    /// 创建使用固定时间源的测验服务。
    /// </summary>
    private static PaperAttemptService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(Now),
            Mock.Of<ILogger<PaperAttemptService>>());
}
