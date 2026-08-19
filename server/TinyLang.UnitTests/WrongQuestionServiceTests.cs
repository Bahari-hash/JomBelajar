using System.Linq;
using System.Security.Claims;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Dtos;
using TinyLang.Constants;
using TinyLang.Endpoints;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证错题列表、详情隔离和单题重做状态转换。
/// </summary>
public sealed class WrongQuestionServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 8, 19, 3, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task ListAndDetailShouldHideAnswersAndEnforceOwnership()
    {
        await using var db = PaperAttemptDictationTests.CreateDbContext();
        var fixture = await PaperAttemptDictationTests.CreatePublishedPaperAsync(db, Ct);
        var attemptService = CreateAttemptService(db);
        var attempt = await attemptService.StartAsync(
            fixture.UserId,
            fixture.Paper.Id,
            Ct);
        await attemptService.SubmitAsync(fixture.UserId, attempt.Attempt.Id, Ct);
        var service = CreateService(db);

        var list = await service.GetListAsync(
            fixture.UserId,
            new PaperWrongQuestionListRequest
            {
                Status = PaperWrongQuestionStatus.Pending,
                Keyword = "listen"
            },
            Ct);
        var item = list.Items.Single();
        var detail = await service.GetByIdAsync(fixture.UserId, item.Id, Ct);
        var crossUser = async () => await service.GetByIdAsync(
            Guid.NewGuid(),
            item.Id,
            Ct);

        item.QuestionId.Should().Be(fixture.Dictation.Id);
        detail.AudioResourceId.Should().Be(fixture.AudioResourceId);
        detail.DictationBlanks.Select(x => x.SortOrder).Should().Equal(0, 1);
        detail.Options.Should().BeEmpty();
        await crossUser.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task RedoShouldMarkCorrectAnswerMasteredAndReturnAnswers()
    {
        await using var db = PaperAttemptDictationTests.CreateDbContext();
        var fixture = await PaperAttemptDictationTests.CreatePublishedPaperAsync(db, Ct);
        var attemptService = CreateAttemptService(db);
        var attempt = await attemptService.StartAsync(
            fixture.UserId,
            fixture.Paper.Id,
            Ct);
        await attemptService.SubmitAsync(fixture.UserId, attempt.Attempt.Id, Ct);
        var wrong = await db.PaperWrongQuestions.SingleAsync(x =>
            x.UserId == fixture.UserId && x.QuestionId == fixture.Dictation.Id,
            Ct);
        var service = CreateService(db);

        var result = await service.RedoAsync(
            fixture.UserId,
            wrong.Id,
            new SavePaperAttemptAnswerRequest
            {
                TextAnswers = [" hello ", "NEW  YORK"]
            },
            Ct);

        result.IsCorrect.Should().BeTrue();
        result.Status.Should().Be(PaperWrongQuestionStatus.Mastered);
        result.RedoCount.Should().Be(1);
        result.MasteredAt.Should().Be(Now);
        result.TextAnswers.Should().Equal(" hello ", "NEW  YORK");
        result.DictationAnswers.Should().Equal("Hello", "New  York");
    }

    [Fact]
    public async Task RedoWrongAnswerShouldRestoreMasteredQuestionToPending()
    {
        await using var db = PaperAttemptDictationTests.CreateDbContext();
        var fixture = await PaperAttemptDictationTests.CreatePublishedPaperAsync(db, Ct);
        var attemptService = CreateAttemptService(db);
        var attempt = await attemptService.StartAsync(
            fixture.UserId,
            fixture.Paper.Id,
            Ct);
        await attemptService.SubmitAsync(fixture.UserId, attempt.Attempt.Id, Ct);
        var wrong = await db.PaperWrongQuestions.SingleAsync(x =>
            x.UserId == fixture.UserId && x.QuestionId == fixture.Dictation.Id,
            Ct);
        var service = CreateService(db);
        await service.RedoAsync(
            fixture.UserId,
            wrong.Id,
            new SavePaperAttemptAnswerRequest
            {
                TextAnswers = ["hello", "New  York"]
            },
            Ct);

        var result = await service.RedoAsync(
            fixture.UserId,
            wrong.Id,
            new SavePaperAttemptAnswerRequest
            {
                TextAnswers = ["hello", "New York"]
            },
            Ct);

        result.IsCorrect.Should().BeFalse();
        result.Status.Should().Be(PaperWrongQuestionStatus.Pending);
        result.RedoCount.Should().Be(2);
        result.WrongCount.Should().Be(2);
        result.MasteredAt.Should().BeNull();
        result.LastWrongAt.Should().Be(Now);
    }

    [Fact]
    public async Task RedoShouldRejectAnswerShapeForDifferentQuestionType()
    {
        await using var db = PaperAttemptDictationTests.CreateDbContext();
        var fixture = await PaperAttemptDictationTests.CreatePublishedPaperAsync(db, Ct);
        var attemptService = CreateAttemptService(db);
        var attempt = await attemptService.StartAsync(
            fixture.UserId,
            fixture.Paper.Id,
            Ct);
        await attemptService.SubmitAsync(fixture.UserId, attempt.Attempt.Id, Ct);
        var wrong = await db.PaperWrongQuestions.SingleAsync(x =>
            x.UserId == fixture.UserId && x.QuestionId == fixture.TrueFalse.Id,
            Ct);
        var service = CreateService(db);

        var action = async () => await service.RedoAsync(
            fixture.UserId,
            wrong.Id,
            new SavePaperAttemptAnswerRequest { TextAnswer = "true" },
            Ct);

        (await action.Should().ThrowAsync<RequestValidationException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperAttemptAnswerShapeInvalid);
    }

    [Fact]
    public async Task EndpointShouldForwardAuthenticatedUserToWrongQuestionService()
    {
        var userId = Guid.NewGuid();
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimNamesExtension.UserId, userId.ToString())
        ], "Test"));
        var request = new PaperWrongQuestionListRequest();
        var expected = new PagedResponse<PaperWrongQuestionListItemResponse>(
            [],
            1,
            20,
            0,
            0);
        var service = new Mock<IWrongQuestionService>();
        service.Setup(value => value.GetListAsync(userId, request, Ct))
            .ReturnsAsync(expected);

        var result = await OnlineQuizEndpoints.GetWrongQuestionsAsync(
            request,
            principal,
            service.Object,
            Ct);

        result.Value.Should().BeSameAs(expected);
        service.Verify(value => value.GetListAsync(userId, request, Ct), Times.Once);
    }

    private static PaperAttemptService CreateAttemptService(
        TinyLang.Database.ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new PaperAttemptDictationTests.FrozenTimeProvider(Now),
            Mock.Of<ILogger<PaperAttemptService>>());

    private static WrongQuestionService CreateService(
        TinyLang.Database.ApplicationDbContext db)
        => new(
            db,
            new PaperAttemptDictationTests.FrozenTimeProvider(Now),
            Mock.Of<ILogger<WrongQuestionService>>());

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
