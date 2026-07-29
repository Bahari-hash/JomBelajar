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
/// 验证试卷完整聚合写入、发布生命周期、历史锁定和安全用户查询。
/// </summary>
public sealed class PaperServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 29, 9, 30, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证创建草稿会规范化字段、计算总分并保存三类题目。
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldNormalizeAndCalculateServerTotal()
    {
        await using var db = CreateDbContext();
        var editorId = Guid.NewGuid();
        var service = CreateService(db);

        var response = await service.CreateDraftAsync(
            editorId,
            CreateCompleteRequest() with
            {
                Title = "  Quiz  ",
                LanguageTag = "EN-us"
            },
            TestContext.Current.CancellationToken);

        response.Title.Should().Be("Quiz");
        response.LanguageTag.Should().Be("en-us");
        response.TotalScore.Should().Be(9);
        response.PassingScore.Should().Be(6);
        response.CreatedById.Should().Be(editorId);
        response.LastEditorId.Should().Be(editorId);
        response.Questions.Should().HaveCount(3);
        response.Questions.Select(value => value.SortOrder)
            .Should().ContainInOrder(0, 1, 2);
    }

    /// <summary>
    /// 验证完整更新保留已有 ID、删除遗漏项并支持无冲突重排。
    /// </summary>
    [Fact]
    public async Task UpdateShouldSynchronizeTargetAndPreserveExistingIds()
    {
        await using var db = CreateDbContext();
        var firstEditorId = Guid.NewGuid();
        var secondEditorId = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            firstEditorId,
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var single = created.Questions.Single(value =>
            value.Type == PaperQuestionType.SingleChoice);
        var trueFalse = created.Questions.Single(value =>
            value.Type == PaperQuestionType.TrueFalse);
        var retainedOption = single.Options.First();

        var request = new UpdatePaperRequest
        {
            Title = "Updated",
            Description = "Description",
            LanguageTag = "en",
            PassingScore = 4,
            ConcurrencyStamp = created.ConcurrencyStamp,
            Questions =
            [
                new PaperQuestionInput
                {
                    Id = trueFalse.Id,
                    Type = PaperQuestionType.TrueFalse,
                    Prompt = "Updated true or false",
                    Points = 4,
                    SortOrder = 0,
                    CorrectBoolean = false
                },
                new PaperQuestionInput
                {
                    Id = single.Id,
                    Type = PaperQuestionType.SingleChoice,
                    Prompt = single.Prompt,
                    Points = 2,
                    SortOrder = 1,
                    Options =
                    [
                        new PaperQuestionOptionInput
                        {
                            Id = retainedOption.Id,
                            Text = "Retained",
                            IsCorrect = true,
                            SortOrder = 1
                        },
                        new PaperQuestionOptionInput
                        {
                            Text = "New",
                            SortOrder = 0
                        }
                    ]
                }
            ]
        };

        var updated = await service.UpdateAsync(
            created.Id,
            secondEditorId,
            request,
            TestContext.Current.CancellationToken);

        updated.Title.Should().Be("Updated");
        updated.TotalScore.Should().Be(6);
        updated.LastEditorId.Should().Be(secondEditorId);
        updated.Questions.Should().HaveCount(2);
        updated.Questions.First().Id.Should().Be(trueFalse.Id);
        updated.Questions.Last().Options.Should().Contain(value =>
            value.Id == retainedOption.Id && value.Text == "Retained");
        updated.Questions.Should().NotContain(value =>
            value.Type == PaperQuestionType.FillBlank);
    }

    /// <summary>
    /// 验证跨试卷 child ID 和过期并发标识均被拒绝。
    /// </summary>
    [Fact]
    public async Task UpdateShouldRejectForeignChildAndStaleConcurrency()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var first = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var second = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest() with { Title = "Second" },
            TestContext.Current.CancellationToken);
        var foreignRequest = ToUpdateRequest(first) with
        {
            Questions =
            [
                ToQuestionInput(first.Questions.First()) with
                {
                    Id = second.Questions.First().Id
                }
            ],
            PassingScore = 1
        };

        var foreign = async () => await service.UpdateAsync(
            first.Id,
            Guid.NewGuid(),
            foreignRequest,
            TestContext.Current.CancellationToken);
        await foreign.Should().ThrowAsync<RequestValidationException>();

        var stale = async () => await service.UpdateAsync(
            first.Id,
            Guid.NewGuid(),
            ToUpdateRequest(first) with { ConcurrencyStamp = Guid.NewGuid() },
            TestContext.Current.CancellationToken);
        await stale.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证发布和下架幂等、首次发布时间保留且目录只返回 Published。
    /// </summary>
    [Fact]
    public async Task PublicationLifecycleShouldBeIdempotentAndDriveCatalogVisibility()
    {
        await using var db = CreateDbContext();
        var editorId = Guid.NewGuid();
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            editorId,
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);

        var published = await service.PublishAsync(
            draft.Id,
            editorId,
            TestContext.Current.CancellationToken);
        var repeated = await service.PublishAsync(
            draft.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        var catalog = await service.GetCatalogAsync(
            new PaperCatalogRequest(),
            TestContext.Current.CancellationToken);

        published.PublishedAt.Should().Be(Now);
        repeated.PublishedAt.Should().Be(Now);
        repeated.ConcurrencyStamp.Should().Be(published.ConcurrencyStamp);
        catalog.Items.Should().ContainSingle();

        var unpublished = await service.UnpublishAsync(
            draft.Id,
            editorId,
            TestContext.Current.CancellationToken);
        var repeatedUnpublish = await service.UnpublishAsync(
            draft.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);
        unpublished.PublishedAt.Should().Be(Now);
        repeatedUnpublish.ConcurrencyStamp.Should().Be(unpublished.ConcurrencyStamp);
        (await service.GetCatalogAsync(
            new PaperCatalogRequest(),
            TestContext.Current.CancellationToken)).Items.Should().BeEmpty();
    }

    /// <summary>
    /// 验证题型不完整的草稿不能发布。
    /// </summary>
    [Fact]
    public async Task PublishShouldRejectIncompleteQuestionShape()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            Guid.NewGuid(),
            new CreatePaperRequest
            {
                Title = "Incomplete",
                LanguageTag = "en",
                Questions =
                [
                    new PaperQuestionInput
                    {
                        Type = PaperQuestionType.SingleChoice,
                        Prompt = "Missing options",
                        Points = 1,
                        SortOrder = 0
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var action = async () => await service.PublishAsync(
            draft.Id,
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证任意测验历史永久锁定内容和删除，但不阻止重新发布。
    /// </summary>
    [Fact]
    public async Task AttemptHistoryShouldLockContentAndDeletionButAllowRepublish()
    {
        await using var db = CreateDbContext();
        var editorId = Guid.NewGuid();
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            editorId,
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var published = await service.PublishAsync(
            draft.Id,
            editorId,
            TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            draft.Id,
            editorId,
            TestContext.Current.CancellationToken);
        db.PaperAttempts.Add(new PaperAttempt
        {
            PaperId = draft.Id,
            UserId = Guid.NewGuid(),
            AttemptNumber = 1,
            PaperTotalScore = published.TotalScore,
            PaperPassingScore = published.PassingScore,
            StartedAt = Now
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var update = async () => await service.UpdateAsync(
            draft.Id,
            editorId,
            ToUpdateRequest(unpublished),
            TestContext.Current.CancellationToken);
        var delete = async () => await service.DeleteAsync(
            draft.Id,
            editorId,
            TestContext.Current.CancellationToken);

        await update.Should().ThrowAsync<ConflictException>();
        await delete.Should().ThrowAsync<ConflictException>();
        (await service.PublishAsync(
            draft.Id,
            editorId,
            TestContext.Current.CancellationToken)).Status
            .Should().Be(PaperPublicationStatus.Published);
    }

    /// <summary>
    /// 创建包含三类完整题目的有效试卷请求。
    /// </summary>
    internal static CreatePaperRequest CreateCompleteRequest()
        => new()
        {
            Title = "Online Quiz",
            Description = "Description",
            Instructions = "Instructions",
            LanguageTag = "en",
            PassingScore = 6,
            Questions =
            [
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.SingleChoice,
                    Prompt = "Choose A",
                    Explanation = "A is correct.",
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
                    Prompt = "The statement is true",
                    Explanation = "It is true.",
                    Points = 3,
                    SortOrder = 1,
                    CorrectBoolean = true
                },
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.FillBlank,
                    Prompt = "Type New York",
                    Explanation = "A city name.",
                    Points = 4,
                    SortOrder = 2,
                    AcceptedAnswers =
                    [
                        new FillBlankAcceptedAnswerInput
                        {
                            Text = "New York",
                            SortOrder = 0
                        },
                        new FillBlankAcceptedAnswerInput
                        {
                            Text = "NYC",
                            SortOrder = 1
                        }
                    ]
                }
            ]
        };

    /// <summary>
    /// 将编辑详情转换为保持全部稳定子项标识的更新请求。
    /// </summary>
    internal static UpdatePaperRequest ToUpdateRequest(EditorPaperResponse response)
        => new()
        {
            Title = response.Title,
            Description = response.Description,
            Instructions = response.Instructions,
            LanguageTag = response.LanguageTag,
            PassingScore = response.PassingScore,
            ConcurrencyStamp = response.ConcurrencyStamp,
            Questions = response.Questions.Select(ToQuestionInput).ToArray()
        };

    /// <summary>
    /// 将编辑题目详情转换为完整题目输入。
    /// </summary>
    private static PaperQuestionInput ToQuestionInput(
        EditorPaperQuestionResponse question)
        => new()
        {
            Id = question.Id,
            Type = question.Type,
            Prompt = question.Prompt,
            Explanation = question.Explanation,
            Points = question.Points,
            SortOrder = question.SortOrder,
            CorrectBoolean = question.CorrectBoolean,
            FillBlankCaseSensitive = question.FillBlankCaseSensitive,
            Options = question.Options.Select(option =>
                new PaperQuestionOptionInput
                {
                    Id = option.Id,
                    Text = option.Text,
                    IsCorrect = option.IsCorrect,
                    SortOrder = option.SortOrder
                }).ToArray(),
            AcceptedAnswers = question.AcceptedAnswers.Select(answer =>
                new FillBlankAcceptedAnswerInput
                {
                    Id = answer.Id,
                    Text = answer.Text,
                    SortOrder = answer.SortOrder
                }).ToArray()
        };

    /// <summary>
    /// 创建使用隔离 InMemory 数据库的应用上下文。
    /// </summary>
    internal static ApplicationDbContext CreateDbContext(string? databaseName = null)
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// 创建使用固定时间源的试卷服务。
    /// </summary>
    internal static PaperService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(Now),
            Mock.Of<ILogger<PaperService>>());
}
