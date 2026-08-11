using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.IntegrationTests;

/// <summary>
/// 使用真实 PostgreSQL 验证在线试卷 migration、约束、查询和并发行为。
/// </summary>
public sealed class OnlineQuizDatabaseIntegrationTests
{
    /// <summary>
    /// 在真实 PostgreSQL 上验证标签精确筛选、目录可见性及 unnest 聚合排序。
    /// </summary>
    [Fact]
    public async Task PaperTagQueriesShouldTranslateAndRespectVisibility()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL online quiz tests.");
        }

        var schema = $"tiny_lang_tags_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteSchemaCommandAsync(
            adminConnection,
            $"CREATE SCHEMA \"{schema}\"",
            TestContext.Current.CancellationToken);

        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(schemaConnection)
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;
            await using (var migrationDb = new ApplicationDbContext(options))
            {
                await migrationDb.Database.MigrateAsync(
                    TestContext.Current.CancellationToken);
            }

            await using var db = new ApplicationDbContext(options);
            var admin = await CreateUserAsync(db, UserRole.Admin);
            var paperService = CreatePaperService(db);
            var firstPublishedId = await CreatePaperWithStatusAsync(
                paperService,
                admin.Id,
                "Tag Search Alpha",
                ["Grammar", "rank-top", "rank-beta"],
                PaperPublicationStatus.Published);
            var secondPublishedId = await CreatePaperWithStatusAsync(
                paperService,
                admin.Id,
                "Tag Search Beta",
                ["grammar", "rank-top", "rank-alpha"],
                PaperPublicationStatus.Published);
            var thirdPublishedId = await CreatePaperWithStatusAsync(
                paperService,
                admin.Id,
                "Tag Search Gamma",
                ["grammar"],
                PaperPublicationStatus.Published);
            var draftId = await CreatePaperWithStatusAsync(
                paperService,
                admin.Id,
                "Tag Search Draft",
                ["draft-only"],
                PaperPublicationStatus.Draft);
            var archivedId = await CreatePaperWithStatusAsync(
                paperService,
                admin.Id,
                "Tag Search Archived",
                ["archived-only"],
                PaperPublicationStatus.Archived);

            var exactCatalog = await paperService.GetCatalogAsync(
                new PaperCatalogRequest { Tag = " GRAMMAR " },
                TestContext.Current.CancellationToken);
            exactCatalog.Items.Select(value => value.Id).Should().BeEquivalentTo(
                [firstPublishedId, secondPublishedId, thirdPublishedId]);
            var partialCatalog = await paperService.GetCatalogAsync(
                new PaperCatalogRequest { Tag = "gram" },
                TestContext.Current.CancellationToken);
            partialCatalog.Items.Should().BeEmpty();

            var draftList = await paperService.GetAdminListAsync(
                new AdminPaperListRequest { Tag = "DRAFT-ONLY" },
                TestContext.Current.CancellationToken);
            draftList.Items.Should().ContainSingle(value => value.Id == draftId);
            var archivedList = await paperService.GetAdminListAsync(
                new AdminPaperListRequest
                {
                    Tag = "ARCHIVED-ONLY",
                    Status = PaperPublicationStatus.Archived
                },
                TestContext.Current.CancellationToken);
            archivedList.Items.Should().ContainSingle(value => value.Id == archivedId);

            var publicTags = await paperService.GetPublicTagListAsync(
                new PaperTagListRequest { PageSize = 20 },
                TestContext.Current.CancellationToken);
            publicTags.Items.Should().Contain(value =>
                value.Name == "grammar" && value.PaperCount == 3);
            publicTags.Items.Should().NotContain(value =>
                value.Name == "draft-only" || value.Name == "archived-only");

            var adminTags = await paperService.GetAdminTagListAsync(
                new PaperTagListRequest { PageSize = 20 },
                TestContext.Current.CancellationToken);
            adminTags.Items.Should().Contain(value =>
                value.Name == "draft-only" && value.PaperCount == 1);
            adminTags.Items.Should().Contain(value =>
                value.Name == "archived-only" && value.PaperCount == 1);

            var rankedTags = await paperService.GetPublicTagListAsync(
                new PaperTagListRequest { Keyword = "RANK", PageSize = 20 },
                TestContext.Current.CancellationToken);
            rankedTags.Items.Select(value => (value.Name, value.PaperCount)).Should()
                .Equal(
                    ("rank-top", 2),
                    ("rank-alpha", 1),
                    ("rank-beta", 1));
        }
        finally
        {
            await ExecuteSchemaCommandAsync(
                adminConnection,
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 在隔离 schema 中验证完整测验流程和 PostgreSQL 专属约束。
    /// </summary>
    [Fact]
    public async Task OnlineQuizSchemaShouldEnforceConstraintsAndTranslateQueries()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL online quiz tests.");
        }

        var schema = $"tiny_lang_quiz_{Guid.NewGuid():N}";
        await using var adminConnection = new NpgsqlConnection(connectionString);
        await adminConnection.OpenAsync(TestContext.Current.CancellationToken);
        await ExecuteSchemaCommandAsync(
            adminConnection,
            $"CREATE SCHEMA \"{schema}\"",
            TestContext.Current.CancellationToken);

        try
        {
            var schemaConnection = new NpgsqlConnectionStringBuilder(connectionString)
            {
                SearchPath = schema
            }.ConnectionString;
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseNpgsql(schemaConnection)
                .AddInterceptors(new AuditableEntityInterceptor())
                .Options;
            await using (var migrationDb = new ApplicationDbContext(options))
            {
                await migrationDb.Database.MigrateAsync(
                    TestContext.Current.CancellationToken);
            }

            Guid paperId;
            Guid userId;
            Guid adminId;
            Guid activeAttemptId;
            Guid trueFalseQuestionId;
            await using (var db = new ApplicationDbContext(options))
            {
                var admin = await CreateUserAsync(db, UserRole.Admin);
                var user = await CreateUserAsync(db, UserRole.User);
                adminId = admin.Id;
                userId = user.Id;
                var paperService = CreatePaperService(db);
                var draft = await paperService.CreateDraftAsync(
                    admin.Id,
                    CreateCompleteRequest(),
                    TestContext.Current.CancellationToken);
                var reordered = await paperService.UpdateAsync(
                    draft.Id,
                    admin.Id,
                    CreateReorderedUpdate(draft),
                    TestContext.Current.CancellationToken);
                var published = await paperService.PublishAsync(
                    reordered.Id,
                    admin.Id,
                    new PaperMutationRequest
                    {
                        ConcurrencyStamp = reordered.ConcurrencyStamp
                    },
                    TestContext.Current.CancellationToken);
                paperId = published.Id;
                published.Tags.Should().Equal("grammar", "a2");
                trueFalseQuestionId = published.Questions.Single(value =>
                    value.Type == PaperQuestionType.TrueFalse).Id;

                var attemptService = CreateAttemptService(db);
                var first = await attemptService.StartAsync(
                    user.Id,
                    paperId,
                    TestContext.Current.CancellationToken);
                await attemptService.SaveAnswerAsync(
                    user.Id,
                    first.Attempt.Id,
                    trueFalseQuestionId,
                    new SavePaperAttemptAnswerRequest { BooleanAnswer = true },
                    TestContext.Current.CancellationToken);
                var result = await attemptService.SubmitAsync(
                    user.Id,
                    first.Attempt.Id,
                    TestContext.Current.CancellationToken);
                result.Score.Should().Be(2);
                result.Questions.Should().HaveCount(3);

                var second = await attemptService.StartAsync(
                    user.Id,
                    paperId,
                    TestContext.Current.CancellationToken);
                activeAttemptId = second.Attempt.Id;
                second.Attempt.AttemptNumber.Should().Be(2);
                (await attemptService.GetHistoryAsync(
                    user.Id,
                    paperId,
                    new PaperAttemptListRequest(),
                    TestContext.Current.CancellationToken)).Items.Should().HaveCount(2);
            }

            await using (var tagDb = new ApplicationDbContext(options))
            {
                var persistedPaper = await tagDb.Papers.AsNoTracking().SingleAsync(
                    value => value.Id == paperId,
                    TestContext.Current.CancellationToken);
                persistedPaper.Tags.Should().Equal("grammar", "a2");
                var tagsProperty = tagDb.Model.FindEntityType(typeof(Paper))!
                    .FindProperty(nameof(Paper.Tags))!;
                tagsProperty.GetColumnType().Should().Be("text[]");
                tagsProperty.IsNullable.Should().BeFalse();
            }

            await VerifyActiveAttemptUniqueAsync(
                options,
                paperId,
                userId);
            await VerifyAnswerUniqueAndShapeAsync(
                options,
                activeAttemptId,
                trueFalseQuestionId);
            await VerifyQuestionCheckConstraintAsync(options, paperId);
            await VerifyConcurrentSubmitAsync(options, userId, activeAttemptId);
            await VerifyAttemptConcurrencyAsync(options, activeAttemptId);
            await VerifyPaperDeleteRestrictedAsync(options, paperId);
            await VerifyArchivedLifecycleAsync(options, adminId, userId);
        }
        finally
        {
            await ExecuteSchemaCommandAsync(
                adminConnection,
                $"DROP SCHEMA IF EXISTS \"{schema}\" CASCADE",
                TestContext.Current.CancellationToken);
        }
    }

    /// <summary>
    /// 验证同一用户和试卷不能持久化第二个 InProgress Attempt。
    /// </summary>
    private static async Task VerifyActiveAttemptUniqueAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid paperId,
        Guid userId)
    {
        await using var db = new ApplicationDbContext(options);
        var paper = await db.Papers.AsNoTracking().SingleAsync(
            value => value.Id == paperId,
            TestContext.Current.CancellationToken);
        db.PaperAttempts.Add(new PaperAttempt
        {
            PaperId = paperId,
            UserId = userId,
            AttemptNumber = 3,
            PaperTotalScore = paper.TotalScore,
            PaperPassingScore = paper.PassingScore,
            StartedAt = DateTimeOffset.UtcNow
        });

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should()
            .Be("IX_paper_attempts_UserId_PaperId");
    }

    /// <summary>
    /// 验证同一 Attempt/Question 答案唯一并执行答案形状 check。
    /// </summary>
    private static async Task VerifyAnswerUniqueAndShapeAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid attemptId,
        Guid questionId)
    {
        await using (var db = new ApplicationDbContext(options))
        {
            db.PaperAttemptAnswers.AddRange(
                new PaperAttemptAnswer
                {
                    AttemptId = attemptId,
                    QuestionId = questionId,
                    BooleanAnswer = true,
                    IsAnswered = true
                },
                new PaperAttemptAnswer
                {
                    AttemptId = attemptId,
                    QuestionId = questionId,
                    BooleanAnswer = false,
                    IsAnswered = true
                });
            var action = async () => await db.SaveChangesAsync(
                TestContext.Current.CancellationToken);
            var exception = await action.Should().ThrowAsync<DbUpdateException>();
            GetConstraintName(exception.Which).Should()
                .Be("IX_paper_attempt_answers_AttemptId_QuestionId");
        }

        await using (var db = new ApplicationDbContext(options))
        {
            db.PaperAttemptAnswers.Add(new PaperAttemptAnswer
            {
                AttemptId = attemptId,
                QuestionId = questionId,
                IsAnswered = true
            });
            var action = async () => await db.SaveChangesAsync(
                TestContext.Current.CancellationToken);
            var exception = await action.Should().ThrowAsync<DbUpdateException>();
            GetConstraintName(exception.Which).Should()
                .Be("CK_paper_attempt_answers_shape");
        }
    }

    /// <summary>
    /// 验证题目分值边界由数据库 check constraint 最终保护。
    /// </summary>
    private static async Task VerifyQuestionCheckConstraintAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid paperId)
    {
        await using var db = new ApplicationDbContext(options);
        db.PaperQuestions.Add(new PaperQuestion
        {
            PaperId = paperId,
            Type = PaperQuestionType.TrueFalse,
            Prompt = "Invalid points",
            CorrectBoolean = true,
            Points = 0,
            SortOrder = 100
        });

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should()
            .Be("CK_paper_questions_points");
    }

    /// <summary>
    /// 验证两个 context 并发 Submit 只产生一组答案并返回同一持久化结果。
    /// </summary>
    private static async Task VerifyConcurrentSubmitAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid userId,
        Guid attemptId)
    {
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var firstService = CreateAttemptService(firstDb);
        var secondService = CreateAttemptService(secondDb);

        var results = await Task.WhenAll(
            firstService.SubmitAsync(
                userId,
                attemptId,
                TestContext.Current.CancellationToken),
            secondService.SubmitAsync(
                userId,
                attemptId,
                TestContext.Current.CancellationToken));

        results[1].Score.Should().Be(results[0].Score);
        results[1].SubmittedAt.Should().Be(results[0].SubmittedAt);
        await using var verificationDb = new ApplicationDbContext(options);
        (await verificationDb.PaperAttemptAnswers.CountAsync(
            value => value.AttemptId == attemptId,
            TestContext.Current.CancellationToken)).Should().Be(3);
    }

    /// <summary>
    /// 验证两个 context 不能用相同原始 stamp 静默覆盖 Attempt。
    /// </summary>
    private static async Task VerifyAttemptConcurrencyAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid attemptId)
    {
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var first = await firstDb.PaperAttempts.SingleAsync(
            value => value.Id == attemptId,
            TestContext.Current.CancellationToken);
        var second = await secondDb.PaperAttempts.SingleAsync(
            value => value.Id == attemptId,
            TestContext.Current.CancellationToken);
        first.ConcurrencyStamp = Guid.NewGuid();
        second.ConcurrencyStamp = Guid.NewGuid();
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = async () => await secondDb.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// 验证存在 Attempt 时数据库 Restrict 阻止 Paper 被删除。
    /// </summary>
    private static async Task VerifyPaperDeleteRestrictedAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid paperId)
    {
        await using var db = new ApplicationDbContext(options);
        var paper = await db.Papers.SingleAsync(
            value => value.Id == paperId,
            TestContext.Current.CancellationToken);
        db.Papers.Remove(paper);

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// 验证 Start 写入 Paper stamp 后旧管理页面不能下架，并验证归档后的历史 Attempt 仍可用。
    /// </summary>
    private static async Task VerifyArchivedLifecycleAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid adminId,
        Guid userId)
    {
        Guid paperId;
        Guid attemptId;
        Guid trueFalseQuestionId;
        AdminPaperResponse published;
        await using (var db = new ApplicationDbContext(options))
        {
            var paperService = CreatePaperService(db);
            var draft = await paperService.CreateDraftAsync(
                adminId,
                CreateCompleteRequest(),
                TestContext.Current.CancellationToken);
            published = await paperService.PublishAsync(
                draft.Id,
                adminId,
                new PaperMutationRequest
                {
                    ConcurrencyStamp = draft.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken);
            paperId = published.Id;
            trueFalseQuestionId = published.Questions.Single(value =>
                value.Type == PaperQuestionType.TrueFalse).Id;
        }

        await using (var startDb = new ApplicationDbContext(options))
        {
            var attempt = await CreateAttemptService(startDb).StartAsync(
                userId,
                paperId,
                TestContext.Current.CancellationToken);
            attemptId = attempt.Attempt.Id;
        }

        await using (var staleDb = new ApplicationDbContext(options))
        {
            var paperService = CreatePaperService(staleDb);
            var staleAction = async () => await paperService.UnpublishAsync(
                paperId,
                adminId,
                new PaperMutationRequest
                {
                    ConcurrencyStamp = published.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken);
            var staleException = await staleAction.Should()
                .ThrowAsync<ConflictException>();
            staleException.Which.ErrorCode.Should()
                .Be(ErrorCodes.PaperConcurrencyConflict);

            var current = await paperService.GetAdminByIdAsync(
                paperId,
                TestContext.Current.CancellationToken);
            var unpublished = await paperService.UnpublishAsync(
                paperId,
                adminId,
                new PaperMutationRequest
                {
                    ConcurrencyStamp = current.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken);
            await paperService.ArchiveAsync(
                paperId,
                adminId,
                new PaperMutationRequest
                {
                    ConcurrencyStamp = unpublished.ConcurrencyStamp
                },
                TestContext.Current.CancellationToken);
        }

        await using (var attemptDb = new ApplicationDbContext(options))
        {
            var attemptService = CreateAttemptService(attemptDb);
            var resumed = await attemptService.StartAsync(
                userId,
                paperId,
                TestContext.Current.CancellationToken);
            resumed.WasCreated.Should().BeFalse();
            resumed.Attempt.Id.Should().Be(attemptId);
            await attemptService.SaveAnswerAsync(
                userId,
                attemptId,
                trueFalseQuestionId,
                new SavePaperAttemptAnswerRequest { BooleanAnswer = true },
                TestContext.Current.CancellationToken);
            await attemptService.ClearAnswerAsync(
                userId,
                attemptId,
                trueFalseQuestionId,
                TestContext.Current.CancellationToken);
            var result = await attemptService.SubmitAsync(
                userId,
                attemptId,
                TestContext.Current.CancellationToken);
            result.Questions.Should().HaveCount(3);
        }

        await using (var verificationDb = new ApplicationDbContext(options))
        {
            var paperService = CreatePaperService(verificationDb);
            var defaultList = await paperService.GetAdminListAsync(
                new AdminPaperListRequest(),
                TestContext.Current.CancellationToken);
            var archivedList = await paperService.GetAdminListAsync(
                new AdminPaperListRequest
                {
                    Status = PaperPublicationStatus.Archived
                },
                TestContext.Current.CancellationToken);
            defaultList.Items.Should().NotContain(value => value.Id == paperId);
            archivedList.Items.Should().ContainSingle(value =>
                value.Id == paperId && value.AttemptCount == 1);
            var catalog = await paperService.GetCatalogAsync(
                new PaperCatalogRequest(),
                TestContext.Current.CancellationToken);
            catalog.Items.Should().NotContain(value => value.Id == paperId);
        }

        await using (var newAttemptDb = new ApplicationDbContext(options))
        {
            var newAttempt = async () => await CreateAttemptService(newAttemptDb)
                .StartAsync(
                    Guid.NewGuid(),
                    paperId,
                    TestContext.Current.CancellationToken);
            await newAttempt.Should().ThrowAsync<NotFoundException>();
        }
    }

    /// <summary>
    /// 创建并保存指定角色的测试用户。
    /// </summary>
    private static async Task<User> CreateUserAsync(
        ApplicationDbContext db,
        UserRole role)
    {
        var user = new User
        {
            Username = $"quiz-{Guid.NewGuid():N}",
            Email = $"quiz-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used",
            Role = role
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>
    /// 创建真实数据库测试使用的试卷服务。
    /// </summary>
    private static PaperService CreatePaperService(ApplicationDbContext db)
        => new(
            db,
            new PostgresDatabaseExceptionClassifier(),
            TimeProvider.System,
            NullLogger<PaperService>.Instance);

    /// <summary>
    /// 创建指定发布状态的规范化标签试卷并返回其标识。
    /// </summary>
    private static async Task<Guid> CreatePaperWithStatusAsync(
        PaperService paperService,
        Guid adminId,
        string title,
        IReadOnlyCollection<string> tags,
        PaperPublicationStatus status)
    {
        var draft = await paperService.CreateDraftAsync(
            adminId,
            CreateCompleteRequest() with { Title = title, Tags = tags },
            TestContext.Current.CancellationToken);
        if (status == PaperPublicationStatus.Draft)
        {
            return draft.Id;
        }

        var published = await paperService.PublishAsync(
            draft.Id,
            adminId,
            new PaperMutationRequest
            {
                ConcurrencyStamp = draft.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        if (status == PaperPublicationStatus.Published)
        {
            return published.Id;
        }

        var unpublished = await paperService.UnpublishAsync(
            published.Id,
            adminId,
            new PaperMutationRequest
            {
                ConcurrencyStamp = published.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var archived = await paperService.ArchiveAsync(
            unpublished.Id,
            adminId,
            new PaperMutationRequest
            {
                ConcurrencyStamp = unpublished.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        return archived.Id;
    }

    /// <summary>
    /// 创建真实数据库测试使用的测验服务。
    /// </summary>
    private static PaperAttemptService CreateAttemptService(ApplicationDbContext db)
        => new(
            db,
            new PostgresDatabaseExceptionClassifier(),
            TimeProvider.System,
            NullLogger<PaperAttemptService>.Instance);

    /// <summary>
    /// 创建包含三类题目的数据库集成测试请求。
    /// </summary>
    private static CreatePaperRequest CreateCompleteRequest()
        => new()
        {
            Title = "Integration Quiz",
            LanguageTag = "en",
            Tags = [" Grammar ", "A2"],
            PassingScorePercentage = 60,
            Questions =
            [
                new PaperQuestionInput
                {
                    Type = PaperQuestionType.SingleChoice,
                    Prompt = "Choose A",
                    Points = 1,
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
                    Prompt = "True",
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

    /// <summary>
    /// 创建会交换现有题目和选项顺序的完整更新请求。
    /// </summary>
    private static UpdatePaperRequest CreateReorderedUpdate(
        AdminPaperResponse draft)
        => new()
        {
            Title = draft.Title,
            Description = draft.Description,
            Instructions = draft.Instructions,
            LanguageTag = draft.LanguageTag,
            Tags = draft.Tags,
            PassingScorePercentage = draft.PassingScorePercentage,
            ConcurrencyStamp = draft.ConcurrencyStamp,
            Questions = draft.Questions.Reverse()
                .Select((question, questionSortOrder) => new PaperQuestionInput
                {
                    Id = question.Id,
                    Type = question.Type,
                    Prompt = question.Prompt,
                    Explanation = question.Explanation,
                    Points = question.Points,
                    SortOrder = questionSortOrder,
                    CorrectBoolean = question.CorrectBoolean,
                    FillBlankCaseSensitive = question.FillBlankCaseSensitive,
                    Options = question.Options.Reverse()
                        .Select((option, optionSortOrder) =>
                            new PaperQuestionOptionInput
                            {
                                Id = option.Id,
                                Text = option.Text,
                                IsCorrect = option.IsCorrect,
                                SortOrder = optionSortOrder
                            })
                        .ToArray(),
                    AcceptedAnswers = question.AcceptedAnswers.Reverse()
                        .Select((answer, answerSortOrder) =>
                            new FillBlankAcceptedAnswerInput
                            {
                                Id = answer.Id,
                                Text = answer.Text,
                                SortOrder = answerSortOrder
                            })
                        .ToArray()
                })
                .ToArray()
        };

    /// <summary>
    /// 从 PostgreSQL update exception 中读取违反的约束名称。
    /// </summary>
    private static string? GetConstraintName(DbUpdateException exception)
        => (exception.InnerException as PostgresException)?.ConstraintName;

    /// <summary>
    /// 在测试数据库执行仅包含服务端生成 schema 名称的 DDL。
    /// </summary>
    private static async Task ExecuteSchemaCommandAsync(
        NpgsqlConnection connection,
        string commandText,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(commandText, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }
}
