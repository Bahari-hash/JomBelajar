using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Policies;
using TinyLang.Services;

namespace TinyLang.IntegrationTests;

/// <summary>
/// 使用真实 PostgreSQL 验证单词背诵 migration、事务、约束和查询翻译。
/// </summary>
public sealed class WordStudyDatabaseIntegrationTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 29, 7, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证完整随机候选查询可以生成 PostgreSQL SQL 且不会退回客户端筛选。
    /// </summary>
    [Fact]
    public void RandomCandidateQueryShouldTranslateForNpgsql()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=localhost;Database=translation_only")
            .Options;
        using var db = new ApplicationDbContext(options);
        var userId = Guid.NewGuid();

        var query = WordVisibilityPolicy.Apply(db.Words.AsNoTracking())
            .Where(word => !db.UserWordProgress.AsNoTracking().Any(
                progress => progress.UserId == userId &&
                    progress.WordId == word.Id))
            .OrderBy(_ => Guid.NewGuid())
            .Take(WordStudyConstraints.MaxWordCount)
            .Select(value => value.Id);
        var sql = query.ToQueryString();

        sql.Should().Contain("NOT EXISTS");
        sql.Should().Contain("ORDER BY gen_random_uuid()");
        sql.Should().Contain("LIMIT");
    }

    /// <summary>
    /// 在隔离 schema 中验证背诵模块全部 PostgreSQL 专属持久化行为。
    /// </summary>
    [Fact]
    public async Task StudySchemaShouldEnforceConstraintsAndAtomicBehavior()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL study tests.");
        }

        var schema = $"tiny_lang_word_study_{Guid.NewGuid():N}";
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

            Guid userId;
            Guid studiedWordId;
            Guid secondSessionId;
            await using (var setupDb = new ApplicationDbContext(options))
            {
                var user = await CreateUserAsync(setupDb, "study-user");
                var first = await CreateVisibleWordAsync(setupDb, user, "first", 0);
                await CreateVisibleWordAsync(setupDb, user, "second", 1);
                await CreateVisibleWordAsync(setupDb, user, "third", 2);
                var hidden = await CreateVisibleWordAsync(setupDb, user, "hidden", 3);
                var hiddenAudio = hidden.Pronunciations.Single().AudioClip
                    ?? throw new InvalidOperationException(
                        "Expected hidden word pronunciation audio.");
                hiddenAudio.PublicationStatus =
                    AudioPublicationStatus.Unpublished;
                await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
                userId = user.Id;

                var service = CreateService(setupDb);
                var firstSession = await service.CreateSessionAsync(
                    userId,
                    new CreateWordStudySessionRequest
                    {
                        WordCount = 1,
                        SelectionMode = WordStudySelectionMode.Random
                    },
                    TestContext.Current.CancellationToken);
                firstSession.ActualCount.Should().Be(1);
                studiedWordId = await setupDb.WordStudySessionItems
                    .Where(value => value.SessionId == firstSession.Id)
                    .Select(value => value.WordId)
                    .SingleAsync(TestContext.Current.CancellationToken);
                studiedWordId.Should().NotBe(hidden.Id);
                first.Id.Should().NotBeEmpty();
            }

            await VerifyConcurrentResultAsync(options, userId, studiedWordId);
            await using (var createDb = new ApplicationDbContext(options))
            {
                var service = CreateService(createDb);
                var secondSession = await service.CreateSessionAsync(
                    userId,
                    new CreateWordStudySessionRequest
                    {
                        WordCount = 10,
                        SelectionMode = WordStudySelectionMode.Sequential
                    },
                    TestContext.Current.CancellationToken);
                secondSession.ActualCount.Should().Be(2);
                secondSessionId = secondSession.Id;
                var selectedIds = await createDb.WordStudySessionItems.AsNoTracking()
                    .Where(value => value.SessionId == secondSessionId)
                    .Select(value => value.WordId)
                    .ToListAsync(TestContext.Current.CancellationToken);
                selectedIds.Should().NotContain(studiedWordId);
            }

            await VerifyActiveSessionUniqueAsync(options, userId);
            await VerifyItemUniqueConstraintsAsync(
                options,
                secondSessionId,
                studiedWordId);
            await VerifyProgressUniqueAndCountCheckAsync(
                options,
                userId,
                studiedWordId);
            await VerifyWordDeleteRestrictedAsync(options, studiedWordId);
            await VerifySessionCascadeAsync(options, userId, secondSessionId);
            await VerifyUserCascadeAsync(options);
            await VerifySessionCountCheckAsync(options);
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
    /// 验证两个真实事务提交同一结果时最终仅累计一次进度。
    /// </summary>
    private static async Task VerifyConcurrentResultAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid userId,
        Guid wordId)
    {
        Guid sessionId;
        Guid itemId;
        await using (var lookupDb = new ApplicationDbContext(options))
        {
            var item = await lookupDb.WordStudySessionItems.AsNoTracking()
                .SingleAsync(
                    value => value.WordId == wordId &&
                        value.Session != null &&
                        value.Session.UserId == userId,
                    TestContext.Current.CancellationToken);
            sessionId = item.SessionId;
            itemId = item.Id;
        }

        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var request = new SubmitWordStudyResultRequest
        {
            Result = WordStudyResult.Remembered
        };
        var results = await Task.WhenAll(
            CreateService(firstDb).SubmitResultAsync(
                userId,
                sessionId,
                itemId,
                request,
                TestContext.Current.CancellationToken),
            CreateService(secondDb).SubmitResultAsync(
                userId,
                sessionId,
                itemId,
                request,
                TestContext.Current.CancellationToken));
        results.Should().OnlyContain(value =>
            value.Status == WordStudySessionStatus.Completed);

        await using var verificationDb = new ApplicationDbContext(options);
        var progress = await verificationDb.UserWordProgress.AsNoTracking()
            .SingleAsync(
                value => value.UserId == userId && value.WordId == wordId,
                TestContext.Current.CancellationToken);
        progress.ReviewCount.Should().Be(1);
        progress.RememberedCount.Should().Be(1);
    }

    /// <summary>
    /// 验证一个用户不能同时保存第二个 Active session。
    /// </summary>
    private static async Task VerifyActiveSessionUniqueAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid userId)
    {
        await using var db = new ApplicationDbContext(options);
        db.WordStudySessions.Add(new WordStudySession
        {
            UserId = userId,
            RequestedCount = 1,
            ActualCount = 1,
            StartedAt = Now
        });

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should()
            .Be("IX_word_study_sessions_UserId");
    }

    /// <summary>
    /// 验证同一 session 的 WordId 和 Position 分别保持唯一。
    /// </summary>
    private static async Task VerifyItemUniqueConstraintsAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid sessionId,
        Guid spareWordId)
    {
        await using (var positionDb = new ApplicationDbContext(options))
        {
            positionDb.WordStudySessionItems.Add(new WordStudySessionItem
            {
                SessionId = sessionId,
                WordId = spareWordId,
                Position = 0
            });
            var action = async () => await positionDb.SaveChangesAsync(
                TestContext.Current.CancellationToken);
            var exception = await action.Should().ThrowAsync<DbUpdateException>();
            GetConstraintName(exception.Which).Should()
                .Be("IX_word_study_session_items_SessionId_Position");
        }

        await using var wordDb = new ApplicationDbContext(options);
        var existingWordId = await wordDb.WordStudySessionItems.AsNoTracking()
            .Where(value => value.SessionId == sessionId)
            .Select(value => value.WordId)
            .FirstAsync(TestContext.Current.CancellationToken);
        wordDb.WordStudySessionItems.Add(new WordStudySessionItem
        {
            SessionId = sessionId,
            WordId = existingWordId,
            Position = 99
        });
        var duplicateWord = async () => await wordDb.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var wordException = await duplicateWord.Should()
            .ThrowAsync<DbUpdateException>();
        GetConstraintName(wordException.Which).Should()
            .Be("IX_word_study_session_items_SessionId_WordId");
    }

    /// <summary>
    /// 验证 progress 复合唯一键和累计计数 check constraint。
    /// </summary>
    private static async Task VerifyProgressUniqueAndCountCheckAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid userId,
        Guid wordId)
    {
        await using (var uniqueDb = new ApplicationDbContext(options))
        {
            uniqueDb.UserWordProgress.Add(new UserWordProgress
            {
                UserId = userId,
                WordId = wordId,
                ReviewCount = 1,
                RememberedCount = 1,
                LastResult = WordStudyResult.Remembered,
                FirstStudiedAt = Now,
                LastStudiedAt = Now
            });
            var action = async () => await uniqueDb.SaveChangesAsync(
                TestContext.Current.CancellationToken);
            var exception = await action.Should().ThrowAsync<DbUpdateException>();
            GetConstraintName(exception.Which).Should()
                .Be("IX_user_word_progress_UserId_WordId");
        }

        await using var checkDb = new ApplicationDbContext(options);
        var ownerId = await checkDb.Users.AsNoTracking()
            .Where(value => value.Id != userId)
            .Select(value => value.Id)
            .FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        if (ownerId == Guid.Empty)
        {
            ownerId = (await CreateUserAsync(checkDb, "check-user")).Id;
        }
        checkDb.UserWordProgress.Add(new UserWordProgress
        {
            UserId = ownerId,
            WordId = wordId,
            ReviewCount = 2,
            RememberedCount = 1,
            ForgottenCount = 0,
            LastResult = WordStudyResult.Remembered,
            FirstStudiedAt = Now,
            LastStudiedAt = Now
        });
        var invalidCounts = async () => await checkDb.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var checkException = await invalidCounts.Should()
            .ThrowAsync<DbUpdateException>();
        GetConstraintName(checkException.Which).Should()
            .Be("CK_user_word_progress_counts");
    }

    /// <summary>
    /// 验证数据库拒绝删除仍被 progress 或 session item 引用的 Word。
    /// </summary>
    private static async Task VerifyWordDeleteRestrictedAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid wordId)
    {
        await using var db = new ApplicationDbContext(options);
        var word = await db.Words.SingleAsync(
            value => value.Id == wordId,
            TestContext.Current.CancellationToken);
        db.Words.Remove(word);

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should().BeOneOf(
            "FK_user_word_progress_words_WordId",
            "FK_word_study_session_items_words_WordId");
    }

    /// <summary>
    /// 验证删除会话会级联删除固定 items，但不会删除 Word。
    /// </summary>
    private static async Task VerifySessionCascadeAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid userId,
        Guid sessionId)
    {
        Guid[] wordIds;
        await using (var abandonDb = new ApplicationDbContext(options))
        {
            wordIds = await abandonDb.WordStudySessionItems.AsNoTracking()
                .Where(value => value.SessionId == sessionId)
                .Select(value => value.WordId)
                .ToArrayAsync(TestContext.Current.CancellationToken);
            await CreateService(abandonDb).AbandonSessionAsync(
                userId,
                sessionId,
                TestContext.Current.CancellationToken);
        }

        await using (var deleteDb = new ApplicationDbContext(options))
        {
            var session = await deleteDb.WordStudySessions.SingleAsync(
                value => value.Id == sessionId,
                TestContext.Current.CancellationToken);
            deleteDb.WordStudySessions.Remove(session);
            await deleteDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verificationDb = new ApplicationDbContext(options);
        (await verificationDb.WordStudySessionItems.AsNoTracking().AnyAsync(
            value => value.SessionId == sessionId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verificationDb.Words.AsNoTracking().CountAsync(
            value => wordIds.Contains(value.Id),
            TestContext.Current.CancellationToken)).Should().Be(wordIds.Length);
    }

    /// <summary>
    /// 验证删除 User 会级联其 progress、session 和 session items。
    /// </summary>
    private static async Task VerifyUserCascadeAsync(
        DbContextOptions<ApplicationDbContext> options)
    {
        Guid userId;
        Guid sessionId;
        await using (var setupDb = new ApplicationDbContext(options))
        {
            var user = await CreateUserAsync(setupDb, "cascade-user");
            var wordIds = await setupDb.Words.AsNoTracking()
                .OrderBy(value => value.Id)
                .Select(value => value.Id)
                .Take(2)
                .ToArrayAsync(TestContext.Current.CancellationToken);
            var session = new WordStudySession
            {
                UserId = user.Id,
                RequestedCount = 1,
                ActualCount = 1,
                StartedAt = Now,
                Items =
                [
                    new WordStudySessionItem
                    {
                        WordId = wordIds[1],
                        Position = 0
                    }
                ]
            };
            setupDb.UserWordProgress.Add(new UserWordProgress
            {
                UserId = user.Id,
                WordId = wordIds[0],
                ReviewCount = 1,
                ForgottenCount = 1,
                LastResult = WordStudyResult.Forgotten,
                FirstStudiedAt = Now,
                LastStudiedAt = Now
            });
            setupDb.WordStudySessions.Add(session);
            await setupDb.SaveChangesAsync(TestContext.Current.CancellationToken);
            userId = user.Id;
            sessionId = session.Id;
        }

        await using (var deleteDb = new ApplicationDbContext(options))
        {
            var user = await deleteDb.Users.SingleAsync(
                value => value.Id == userId,
                TestContext.Current.CancellationToken);
            deleteDb.Users.Remove(user);
            await deleteDb.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using var verificationDb = new ApplicationDbContext(options);
        (await verificationDb.UserWordProgress.AnyAsync(
            value => value.UserId == userId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verificationDb.WordStudySessions.AnyAsync(
            value => value.UserId == userId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verificationDb.WordStudySessionItems.AnyAsync(
            value => value.SessionId == sessionId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
    }

    /// <summary>
    /// 验证 session 请求数量和实际数量的数据库 check constraint。
    /// </summary>
    private static async Task VerifySessionCountCheckAsync(
        DbContextOptions<ApplicationDbContext> options)
    {
        await using var db = new ApplicationDbContext(options);
        var user = await CreateUserAsync(db, "session-check-user");
        db.WordStudySessions.Add(new WordStudySession
        {
            UserId = user.Id,
            RequestedCount = 1,
            ActualCount = 0,
            StartedAt = Now
        });

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should()
            .Be("CK_word_study_sessions_counts");
    }

    /// <summary>
    /// 创建并保存具有唯一账户字段的测试用户。
    /// </summary>
    private static async Task<User> CreateUserAsync(
        ApplicationDbContext db,
        string prefix)
    {
        var user = new User
        {
            Username = $"{prefix}-{Guid.NewGuid():N}",
            Email = $"{prefix}-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used"
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>
    /// 创建并保存满足共享用户可见性规则的完整词条。
    /// </summary>
    private static async Task<Word> CreateVisibleWordAsync(
        ApplicationDbContext db,
        User owner,
        string headword,
        int publishedOrder)
    {
        var source = new MediaResource
        {
            UploaderId = owner.Id,
            Uploader = owner,
            ObjectName = $"word-study-tests/{Guid.NewGuid():N}.mp3",
            OriginalName = "word.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var audio = new AudioClip
        {
            CreatedById = owner.Id,
            CreatedBy = owner,
            LastEditorId = owner.Id,
            LastEditor = owner,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = headword,
            LanguageTag = "en",
            Kind = AudioClipKind.WordPronunciation,
            ProcessingStatus = AudioProcessingStatus.Ready,
            PublicationStatus = AudioPublicationStatus.Published
        };
        var word = new Word
        {
            LanguageTag = "en",
            Headword = headword,
            NormalizedHeadword = headword.ToUpperInvariant(),
            Status = WordPublicationStatus.Published,
            PublishedAt = Now.AddMinutes(-publishedOrder),
            CreatedById = owner.Id,
            LastEditorId = owner.Id,
            Senses =
            [
                new WordSense
                {
                    PartOfSpeech = PartOfSpeech.Noun,
                    Definition = headword,
                    DefinitionLanguageTag = "en",
                    SortOrder = 0,
                    Examples =
                    [
                        new ExampleSentence
                        {
                            Sentence = headword,
                            LanguageTag = "en",
                            Translation = headword,
                            TranslationLanguageTag = "en",
                            SortOrder = 0
                        }
                    ]
                }
            ],
            Pronunciations =
            [
                new WordPronunciation
                {
                    AudioClipId = audio.Id,
                    AudioClip = audio,
                    IsDefault = true,
                    SortOrder = 0
                }
            ]
        };
        db.Words.Add(word);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return word;
    }

    /// <summary>
    /// 创建使用 PostgreSQL 异常分类和固定时间的真实数据库服务。
    /// </summary>
    private static WordStudyService CreateService(ApplicationDbContext db)
        => new(
            db,
            new PostgresDatabaseExceptionClassifier(),
            new FixedTimeProvider(Now),
            NullLogger<WordStudyService>.Instance);

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

    /// <summary>
    /// 为真实数据库测试提供确定性的 UTC 时间。
    /// </summary>
    /// <param name="utcNow">服务应返回的固定时间。</param>
    private sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        /// <inheritdoc />
        public override DateTimeOffset GetUtcNow() => utcNow;
    }
}
