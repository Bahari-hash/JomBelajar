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
        var adminId = Guid.NewGuid();
        var service = CreateService(db);

        var response = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with
            {
                Title = "  Quiz  ",
                LanguageTag = "EN-us",
                Tags = [" Grammar ", "A2"]
            },
            TestContext.Current.CancellationToken);

        response.Title.Should().Be("Quiz");
        response.LanguageTag.Should().Be("en-us");
        response.Tags.Should().Equal("grammar", "a2");
        (await db.Papers.SingleAsync(TestContext.Current.CancellationToken)).Tags.Should().Equal("grammar", "a2");
        response.TotalScore.Should().Be(9);
        response.PassingScorePercentage.Should().Be(60);
        response.PassingScore.Should().Be(6);
        response.CreatedBy.Id.Should().Be(adminId);
        response.LastEditor.Id.Should().Be(adminId);
        response.Questions.Should().HaveCount(3);
        response.Questions.Select(value => value.SortOrder)
            .Should().ContainInOrder(0, 1, 2);
    }

    /// <summary>
    /// 验证及格分按照总分与百分比计算，并对非整数结果向上取整。
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldRoundPassingScoreUp()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = CreateCompleteRequest();

        var response = await service.CreateDraftAsync(
            Guid.NewGuid(),
            request with
            {
                Questions = request.Questions.Select(question => question with
                {
                    Points = 1
                }).ToArray()
            },
            TestContext.Current.CancellationToken);

        response.TotalScore.Should().Be(3);
        response.PassingScorePercentage.Should().Be(60);
        response.PassingScore.Should().Be(2);
    }

    /// <summary>
    /// 验证完整更新保留已有 ID、删除遗漏项并支持无冲突重排。
    /// </summary>
    [Fact]
    public async Task UpdateShouldSynchronizeTargetAndPreserveExistingIds()
    {
        await using var db = CreateDbContext();
        var firstAdminId = Guid.NewGuid();
        var secondAdminId = Guid.NewGuid();
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            firstAdminId,
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
            Tags = ["B1", " Reading "],
            PassingScorePercentage = 50,
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
            secondAdminId,
            request,
            TestContext.Current.CancellationToken);

        updated.Title.Should().Be("Updated");
        updated.Tags.Should().Equal("b1", "reading");
        updated.TotalScore.Should().Be(6);
        updated.PassingScorePercentage.Should().Be(50);
        updated.PassingScore.Should().Be(3);
        updated.LastEditor.Id.Should().Be(secondAdminId);
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
            PassingScorePercentage = 60
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
        var adminId = Guid.NewGuid();
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);

        var published = await service.PublishAsync(
            draft.Id,
            adminId,
            Mutation(draft),
            TestContext.Current.CancellationToken);
        var repeated = await service.PublishAsync(
            draft.Id,
            Guid.NewGuid(),
            Mutation(published),
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
            adminId,
            Mutation(published),
            TestContext.Current.CancellationToken);
        var repeatedUnpublish = await service.UnpublishAsync(
            draft.Id,
            Guid.NewGuid(),
            Mutation(unpublished),
            TestContext.Current.CancellationToken);
        unpublished.PublishedAt.Should().Be(Now);
        repeatedUnpublish.ConcurrencyStamp.Should().Be(unpublished.ConcurrencyStamp);
        (await service.GetCatalogAsync(
            new PaperCatalogRequest(),
            TestContext.Current.CancellationToken)).Items.Should().BeEmpty();
    }

    /// <summary>
    /// 验证用户目录按规范化标签精确筛选，并与关键词筛选组合。
    /// </summary>
    [Fact]
    public async Task CatalogShouldFilterByExactCaseInsensitiveTagAndKeyword()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var adminId = Guid.NewGuid();
        var matching = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "Target CET", Tags = ["cet"] },
            TestContext.Current.CancellationToken);
        var prefixOnly = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "Target CET-4", Tags = ["cet-4"] },
            TestContext.Current.CancellationToken);
        var keywordMismatch = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "CET Overview", Tags = ["cet"] },
            TestContext.Current.CancellationToken);
        await service.PublishAsync(
            matching.Id,
            adminId,
            Mutation(matching),
            TestContext.Current.CancellationToken);
        await service.PublishAsync(
            prefixOnly.Id,
            adminId,
            Mutation(prefixOnly),
            TestContext.Current.CancellationToken);
        await service.PublishAsync(
            keywordMismatch.Id,
            adminId,
            Mutation(keywordMismatch),
            TestContext.Current.CancellationToken);

        var result = await service.GetCatalogAsync(
            new PaperCatalogRequest { Tag = "  CeT  ", Keyword = "target" },
            TestContext.Current.CancellationToken);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(value => value.Id == matching.Id);
    }

    /// <summary>
    /// 验证管理员列表按规范化标签精确筛选，并与关键词筛选组合。
    /// </summary>
    [Fact]
    public async Task AdminListShouldFilterByExactCaseInsensitiveTagAndKeyword()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var adminId = Guid.NewGuid();
        var matching = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "Target CET", Tags = ["cet"] },
            TestContext.Current.CancellationToken);
        await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "Target CET-4", Tags = ["cet-4"] },
            TestContext.Current.CancellationToken);
        await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "CET Overview", Tags = ["cet"] },
            TestContext.Current.CancellationToken);

        var result = await service.GetAdminListAsync(
            new AdminPaperListRequest { Tag = "  CeT  ", Keyword = "target" },
            TestContext.Current.CancellationToken);

        result.TotalCount.Should().Be(1);
        result.Items.Should().ContainSingle(value => value.Id == matching.Id);
    }

    /// <summary>
    /// 验证公开标签目录只统计已发布试卷，管理员目录包含草稿并稳定排序。
    /// </summary>
    [Fact]
    public async Task TagDirectoriesShouldAggregateVisibilitySearchAndOrdering()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var adminId = Guid.NewGuid();
        var first = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "First", Tags = ["grammar", "cet-4"] },
            TestContext.Current.CancellationToken);
        var second = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "Second", Tags = ["grammar"] },
            TestContext.Current.CancellationToken);
        await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = "Draft", Tags = ["draft-only"] },
            TestContext.Current.CancellationToken);
        await service.PublishAsync(
            first.Id,
            adminId,
            Mutation(first),
            TestContext.Current.CancellationToken);
        await service.PublishAsync(
            second.Id,
            adminId,
            Mutation(second),
            TestContext.Current.CancellationToken);

        var publicTags = await service.GetPublicTagListAsync(
            new PaperTagListRequest(),
            TestContext.Current.CancellationToken);
        var searched = await service.GetPublicTagListAsync(
            new PaperTagListRequest { Keyword = "GRAM" },
            TestContext.Current.CancellationToken);
        var adminTags = await service.GetAdminTagListAsync(
            new PaperTagListRequest(),
            TestContext.Current.CancellationToken);

        publicTags.Items.Should().Equal(
            new PaperTagSummaryResponse("grammar", 2),
            new PaperTagSummaryResponse("cet-4", 1));
        searched.Items.Should().ContainSingle().Which
            .Should().Be(new PaperTagSummaryResponse("grammar", 2));
        adminTags.Items.Should().Contain(value =>
            value.Name == "draft-only" && value.PaperCount == 1);
    }

    /// <summary>
    /// 验证 PostgreSQL 标签目录查询能够通过 provider 翻译并到达连接阶段。
    /// </summary>
    [Fact]
    public async Task PostgreSqlTagDirectoryShouldNotFailDuringLinqTranslation()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=127.0.0.1;Port=1;Database=tiny_lang_translation;" +
                "Username=test;Password=test;Timeout=1;Command Timeout=1")
            .Options;
        await using var db = new ApplicationDbContext(options);
        var service = CreateService(db);
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(200));

        var exception = await Record.ExceptionAsync(() =>
            service.GetAdminTagListAsync(
                new PaperTagListRequest(),
                cancellation.Token));

        exception.Should().NotBeNull();
        exception!.ToString().Should().NotContain("could not be translated");
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
            Mutation(draft),
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
        var adminId = Guid.NewGuid();
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var published = await service.PublishAsync(
            draft.Id,
            adminId,
            Mutation(draft),
            TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            draft.Id,
            adminId,
            Mutation(published),
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
            adminId,
            ToUpdateRequest(unpublished),
            TestContext.Current.CancellationToken);
        var delete = async () => await service.DeleteAsync(
            draft.Id,
            adminId,
            Mutation(unpublished),
            TestContext.Current.CancellationToken);

        await update.Should().ThrowAsync<ConflictException>();
        await delete.Should().ThrowAsync<ConflictException>();
        (await service.PublishAsync(
            draft.Id,
            adminId,
            Mutation(unpublished),
            TestContext.Current.CancellationToken)).Status
            .Should().Be(PaperPublicationStatus.Published);
    }

    /// <summary>
    /// 验证发布检查返回稳定字段问题且不修改审计、时间或并发标识。
    /// </summary>
    [Fact]
    public async Task ValidateShouldReturnStructuredIssuesWithoutMutation()
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
                    },
                    new PaperQuestionInput
                    {
                        Type = PaperQuestionType.TrueFalse,
                        Prompt = "Missing answer",
                        Points = 1,
                        SortOrder = 1
                    },
                    new PaperQuestionInput
                    {
                        Type = PaperQuestionType.FillBlank,
                        Prompt = "Missing answers",
                        Points = 1,
                        SortOrder = 2
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var validation = await service.ValidateAsync(
            draft.Id,
            Mutation(draft),
            TestContext.Current.CancellationToken);
        var after = await service.GetAdminByIdAsync(
            draft.Id,
            TestContext.Current.CancellationToken);

        validation.IsValid.Should().BeFalse();
        validation.Issues.Select(value => value.Field).Should().ContainInOrder(
            "questions[0].options",
            "questions[1].correctBoolean",
            "questions[2].acceptedAnswers");
        validation.Issues.Should().OnlyContain(value =>
            value.QuestionId.HasValue && value.Message.Length > 0);
        after.ConcurrencyStamp.Should().Be(draft.ConcurrencyStamp);
        after.LastEditor.Should().Be(draft.LastEditor);
        after.UpdatedAt.Should().Be(draft.UpdatedAt);

        var publish = async () => await service.PublishAsync(
            draft.Id,
            Guid.NewGuid(),
            Mutation(draft),
            TestContext.Current.CancellationToken);
        (await publish.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperPublishRequirementsNotMet);
    }

    /// <summary>
    /// 验证归档是默认隐藏的终态并保留审计和测验历史摘要。
    /// </summary>
    [Fact]
    public async Task ArchiveShouldPreserveHistoryAndDriveAdminListVisibility()
    {
        await using var db = CreateDbContext();
        var admin = new User
        {
            Email = "paper-admin@example.test",
            PasswordHash = "not-used",
            Nickname = "Paper Admin",
            Role = UserRole.Admin
        };
        db.Users.Add(admin);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            admin.Id,
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var published = await service.PublishAsync(
            draft.Id,
            admin.Id,
            Mutation(draft),
            TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            published.Id,
            admin.Id,
            Mutation(published),
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

        var archived = await service.ArchiveAsync(
            draft.Id,
            admin.Id,
            Mutation(unpublished),
            TestContext.Current.CancellationToken);
        var defaultList = await service.GetAdminListAsync(
            new AdminPaperListRequest(),
            TestContext.Current.CancellationToken);
        var archivedList = await service.GetAdminListAsync(
            new AdminPaperListRequest { Status = PaperPublicationStatus.Archived },
            TestContext.Current.CancellationToken);

        archived.Status.Should().Be(PaperPublicationStatus.Archived);
        archived.ArchivedAt.Should().Be(Now);
        archived.AttemptCount.Should().Be(1);
        archived.CreatedBy.Nickname.Should().Be("Paper Admin");
        defaultList.Items.Should().BeEmpty();
        archivedList.Items.Should().ContainSingle(value =>
            value.Id == draft.Id && value.AttemptCount == 1 &&
            value.CreatedBy.Nickname == "Paper Admin");

        var repeated = async () => await service.ArchiveAsync(
            draft.Id,
            admin.Id,
            Mutation(archived),
            TestContext.Current.CancellationToken);
        (await repeated.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperArchiveConflict);
    }

    /// <summary>
    /// 验证过期 stamp 在状态和历史冲突之前统一返回并发冲突。
    /// </summary>
    [Fact]
    public async Task MutationsShouldCheckStampBeforeStateAndHistory()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(),
            TestContext.Current.CancellationToken);
        var published = await service.PublishAsync(
            draft.Id,
            Guid.NewGuid(),
            Mutation(draft),
            TestContext.Current.CancellationToken);

        var staleUnpublish = async () => await service.UnpublishAsync(
            draft.Id,
            Guid.NewGuid(),
            Mutation(draft),
            TestContext.Current.CancellationToken);
        var staleDelete = async () => await service.DeleteAsync(
            draft.Id,
            Guid.NewGuid(),
            Mutation(draft),
            TestContext.Current.CancellationToken);

        (await staleUnpublish.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperConcurrencyConflict);
        (await staleDelete.Should().ThrowAsync<ConflictException>())
            .Which.ErrorCode.Should().Be(ErrorCodes.PaperConcurrencyConflict);
        published.Status.Should().Be(PaperPublicationStatus.Published);
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
            PassingScorePercentage = 60,
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
    internal static UpdatePaperRequest ToUpdateRequest(AdminPaperResponse response)
        => new()
        {
            Title = response.Title,
            Description = response.Description,
            Instructions = response.Instructions,
            LanguageTag = response.LanguageTag,
            PassingScorePercentage = response.PassingScorePercentage,
            ConcurrencyStamp = response.ConcurrencyStamp,
            Questions = response.Questions.Select(ToQuestionInput).ToArray()
        };

    /// <summary>
    /// 创建状态动作和硬删除使用的当前并发前置条件。
    /// </summary>
    internal static PaperMutationRequest Mutation(AdminPaperResponse response)
        => new() { ConcurrencyStamp = response.ConcurrencyStamp };

    /// <summary>
    /// 将编辑题目详情转换为完整题目输入。
    /// </summary>
    private static PaperQuestionInput ToQuestionInput(
        AdminPaperQuestionResponse question)
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
