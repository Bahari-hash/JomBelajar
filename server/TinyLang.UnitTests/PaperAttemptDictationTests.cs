using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证听写答案保存、恢复、判分和错题收录规则。
/// </summary>
public sealed class PaperAttemptDictationTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 8, 19, 2, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task SaveDictationShouldRequireExactBlankCount()
    {
        await using var db = CreateDbContext();
        var fixture = await CreatePublishedPaperAsync(db, Ct);
        var service = CreateAttemptService(db);
        var attempt = await service.StartAsync(fixture.UserId, fixture.Paper.Id, Ct);

        var action = async () => await service.SaveAnswerAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            fixture.Dictation.Id,
            new SavePaperAttemptAnswerRequest { TextAnswers = ["hello"] },
            Ct);

        (await action.Should().ThrowAsync<RequestValidationException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperAttemptAnswerShapeInvalid);
    }

    [Fact]
    public async Task DictationShouldRestoreSafeAudioAndAwardAllPointsWhenEveryBlankMatches()
    {
        await using var db = CreateDbContext();
        var fixture = await CreatePublishedPaperAsync(db, Ct);
        var service = CreateAttemptService(db);
        var attempt = await service.StartAsync(fixture.UserId, fixture.Paper.Id, Ct);

        await service.SaveAnswerAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            fixture.Dictation.Id,
            new SavePaperAttemptAnswerRequest
            {
                TextAnswers = [" HELLO ", "new  york"]
            },
            Ct);

        var restored = await service.GetAttemptAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            Ct);
        var result = await service.SubmitAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            Ct);

        var safeQuestion = restored.Questions.Single(x => x.Id == fixture.Dictation.Id);
        safeQuestion.AudioResourceId.Should().Be(fixture.AudioResourceId);
        safeQuestion.DictationBlanks.Select(x => x.SortOrder).Should().Equal(0, 1);
        safeQuestion.SavedAnswer!.TextAnswers.Should().Equal(" HELLO ", "new  york");

        var graded = result.Questions.Single(x => x.QuestionId == fixture.Dictation.Id);
        graded.IsCorrect.Should().BeTrue();
        graded.AwardedPoints.Should().Be(5);
        graded.TextAnswers.Should().Equal(" HELLO ", "new  york");
        graded.DictationAnswers.Should().Equal("Hello", "New  York");
        graded.AudioResourceId.Should().Be(fixture.AudioResourceId);
    }

    [Fact]
    public async Task DictationShouldKeepInternalSpacesSignificantAndAwardZeroForPartialMatch()
    {
        await using var db = CreateDbContext();
        var fixture = await CreatePublishedPaperAsync(db, Ct);
        var service = CreateAttemptService(db);
        var attempt = await service.StartAsync(fixture.UserId, fixture.Paper.Id, Ct);

        await service.SaveAnswerAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            fixture.Dictation.Id,
            new SavePaperAttemptAnswerRequest
            {
                TextAnswers = ["hello", "New York"]
            },
            Ct);

        var result = await service.SubmitAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            Ct);
        var graded = result.Questions.Single(x => x.QuestionId == fixture.Dictation.Id);

        graded.IsCorrect.Should().BeFalse();
        graded.AwardedPoints.Should().Be(0);
    }

    [Fact]
    public async Task DictationShouldTreatExactCountEmptyValuesAsAnsweredButIncorrect()
    {
        await using var db = CreateDbContext();
        var fixture = await CreatePublishedPaperAsync(db, Ct);
        var service = CreateAttemptService(db);
        var attempt = await service.StartAsync(fixture.UserId, fixture.Paper.Id, Ct);
        await service.SaveAnswerAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            fixture.Dictation.Id,
            new SavePaperAttemptAnswerRequest { TextAnswers = ["", ""] },
            Ct);

        var result = await service.SubmitAsync(
            fixture.UserId,
            attempt.Attempt.Id,
            Ct);
        var graded = result.Questions.Single(x => x.QuestionId == fixture.Dictation.Id);

        graded.IsAnswered.Should().BeTrue();
        graded.IsCorrect.Should().BeFalse();
        graded.AwardedPoints.Should().Be(0);
    }

    [Fact]
    public async Task SubmitShouldUpsertIncorrectAndUnansweredQuestions()
    {
        await using var db = CreateDbContext();
        var fixture = await CreatePublishedPaperAsync(db, Ct);
        var service = CreateAttemptService(db);
        var firstAttempt = await service.StartAsync(
            fixture.UserId,
            fixture.Paper.Id,
            Ct);
        await service.SaveAnswerAsync(
            fixture.UserId,
            firstAttempt.Attempt.Id,
            fixture.Dictation.Id,
            new SavePaperAttemptAnswerRequest { TextAnswers = ["wrong", "answers"] },
            Ct);
        await service.SubmitAsync(fixture.UserId, firstAttempt.Attempt.Id, Ct);

        var secondAttempt = await service.StartAsync(
            fixture.UserId,
            fixture.Paper.Id,
            Ct);
        await service.SubmitAsync(fixture.UserId, secondAttempt.Attempt.Id, Ct);

        var wrongQuestions = await db.PaperWrongQuestions.AsNoTracking()
            .OrderBy(x => x.QuestionId)
            .ToListAsync(Ct);

        wrongQuestions.Should().HaveCount(2);
        wrongQuestions.Should().OnlyContain(x =>
            x.UserId == fixture.UserId &&
            x.Status == PaperWrongQuestionStatus.Pending &&
            x.WrongCount == 2 &&
            x.FirstWrongAt == Now &&
            x.LastWrongAt == Now);
    }

    internal static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    internal static PaperAttemptService CreateAttemptService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new FrozenTimeProvider(Now),
            Mock.Of<ILogger<PaperAttemptService>>());

    internal static async Task<QuizFixture> CreatePublishedPaperAsync(
        ApplicationDbContext db,
        CancellationToken cancellationToken)
    {
        var user = new User
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash"
        };
        var admin = new User
        {
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };
        var audioResourceId = Guid.NewGuid();
        var dictation = new PaperQuestion
        {
            Type = PaperQuestionType.Dictation,
            Prompt = "Listen and type",
            Points = 5,
            SortOrder = 0,
            AudioResourceId = audioResourceId,
            DictationBlanks =
            [
                new PaperDictationBlank
                {
                    Answer = "Hello",
                    NormalizedAnswer = "HELLO",
                    SortOrder = 0
                },
                new PaperDictationBlank
                {
                    Answer = "New  York",
                    NormalizedAnswer = "NEW  YORK",
                    SortOrder = 1
                }
            ]
        };
        var trueFalse = new PaperQuestion
        {
            Type = PaperQuestionType.TrueFalse,
            Prompt = "True or false",
            Points = 3,
            SortOrder = 1,
            CorrectBoolean = true
        };
        var paper = new Paper
        {
            Title = "Quiz",
            Status = PaperPublicationStatus.Published,
            PassingScorePercentage = 60,
            PassingScore = 5,
            TotalScore = 8,
            CreatedById = admin.Id,
            LastEditorId = admin.Id,
            PublishedAt = Now,
            Questions = [dictation, trueFalse]
        };
        db.Users.AddRange(user, admin);
        db.Papers.Add(paper);
        await db.SaveChangesAsync(cancellationToken);
        return new QuizFixture(user.Id, paper, dictation, trueFalse, audioResourceId);
    }

    internal sealed record QuizFixture(
        Guid UserId,
        Paper Paper,
        PaperQuestion Dictation,
        PaperQuestion TrueFalse,
        Guid AudioResourceId);

    internal sealed class FrozenTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => utcNow;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;
}
