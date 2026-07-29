using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Infrastructure;
using TinyLang.Services;

namespace TinyLang.IntegrationTests;

/// <summary>
/// 使用真实 PostgreSQL 验证词条 migration、约束、并发和复杂查询翻译。
/// </summary>
public sealed class WordDatabaseIntegrationTests
{
    /// <summary>
    /// 在隔离 schema 中验证词条的 PostgreSQL 专属持久化行为。
    /// </summary>
    [Fact]
    public async Task WordSchemaShouldEnforceConstraintsAndTranslateUserQueries()
    {
        var connectionString = Environment.GetEnvironmentVariable(
            "TINYLANG_POSTGRES_TEST_CONNECTION_STRING");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Assert.Skip(
                "Set TINYLANG_POSTGRES_TEST_CONNECTION_STRING to run PostgreSQL word tests.");
        }

        var schema = $"tiny_lang_word_{Guid.NewGuid():N}";
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

            Guid wordId;
            Guid firstAudioId;
            Guid secondAudioId;
            Guid thirdAudioId;
            await using (var db = new ApplicationDbContext(options))
            {
                var user = await CreateUserAsync(db);
                var firstAudio = await CreateAudioAsync(
                    db, user, AudioClipKind.WordPronunciation, "en");
                var secondAudio = await CreateAudioAsync(
                    db, user, AudioClipKind.WordPronunciation, "en-US");
                var thirdAudio = await CreateAudioAsync(
                    db, user, AudioClipKind.WordPronunciation, "en-GB");
                var service = CreateService(db);
                var draft = await service.CreateDraftAsync(
                    user.Id,
                    CreateCompleteRequest(firstAudio.Id, secondAudio.Id),
                    TestContext.Current.CancellationToken);
                var published = await service.PublishAsync(
                    draft.Id,
                    user.Id,
                    TestContext.Current.CancellationToken);
                var page = await service.GetUserListAsync(
                    new WordListRequest { Language = "EN" },
                    TestContext.Current.CancellationToken);

                page.Items.Should().ContainSingle();
                var unpublished = await service.UnpublishAsync(
                    draft.Id,
                    user.Id,
                    TestContext.Current.CancellationToken);
                var updated = await service.UpdateAsync(
                    draft.Id,
                    user.Id,
                    CreateDefaultSwapRequest(
                        unpublished,
                        firstAudio.Id,
                        secondAudio.Id),
                    TestContext.Current.CancellationToken);
                updated.Pronunciations.Single(value => value.AudioClipId == secondAudio.Id)
                    .IsDefault.Should().BeTrue();
                updated.Pronunciations.Single(value => value.AudioClipId == secondAudio.Id)
                    .SortOrder.Should().Be(0);
                await service.PublishAsync(
                    draft.Id,
                    user.Id,
                    TestContext.Current.CancellationToken);
                published.PublishedAt.Should().Be(updated.PublishedAt);
                wordId = draft.Id;
                firstAudioId = firstAudio.Id;
                secondAudioId = secondAudio.Id;
                thirdAudioId = thirdAudio.Id;
            }

            await VerifyHeadwordUniqueConstraintAsync(options);
            await VerifyDefaultPronunciationConstraintAsync(
                options, wordId, thirdAudioId);
            await VerifyConcurrencyAsync(options, wordId);
            await VerifyAudioDeleteRestrictedAsync(options, firstAudioId);
            await VerifyCascadeAndAudioRestrictAsync(options, firstAudioId);
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
    /// 验证 language + normalized headword 复合唯一约束。
    /// </summary>
    private static async Task VerifyHeadwordUniqueConstraintAsync(
        DbContextOptions<ApplicationDbContext> options)
    {
        await using var db = new ApplicationDbContext(options);
        var userId = await db.Users.Select(value => value.Id)
            .FirstAsync(TestContext.Current.CancellationToken);
        db.Words.Add(new Word
        {
            LanguageTag = "en",
            Headword = "HELLO",
            NormalizedHeadword = "HELLO",
            CreatedById = userId,
            LastEditorId = userId
        });

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should()
            .Be("IX_words_LanguageTag_NormalizedHeadword");
    }

    /// <summary>
    /// 验证每个词条最多一个默认发音的 partial unique index。
    /// </summary>
    private static async Task VerifyDefaultPronunciationConstraintAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid wordId,
        Guid secondAudioId)
    {
        await using var db = new ApplicationDbContext(options);
        db.WordPronunciations.Add(new WordPronunciation
        {
            WordId = wordId,
            AudioClipId = secondAudioId,
            IsDefault = true,
            SortOrder = 1
        });

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        var exception = await action.Should().ThrowAsync<DbUpdateException>();
        GetConstraintName(exception.Which).Should()
            .Be("IX_word_pronunciations_WordId");
    }

    /// <summary>
    /// 验证两个 context 不能以相同原始并发戳静默覆盖词条。
    /// </summary>
    private static async Task VerifyConcurrencyAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid wordId)
    {
        await using var firstDb = new ApplicationDbContext(options);
        await using var secondDb = new ApplicationDbContext(options);
        var first = await firstDb.Words.SingleAsync(
            value => value.Id == wordId,
            TestContext.Current.CancellationToken);
        var second = await secondDb.Words.SingleAsync(
            value => value.Id == wordId,
            TestContext.Current.CancellationToken);
        first.ConcurrencyStamp = Guid.NewGuid();
        second.ConcurrencyStamp = Guid.NewGuid();
        await firstDb.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = async () => await secondDb.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    /// <summary>
    /// 验证仍被词条发音引用的 AudioClip 不能被数据库删除。
    /// </summary>
    private static async Task VerifyAudioDeleteRestrictedAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid audioId)
    {
        await using var db = new ApplicationDbContext(options);
        var audio = await db.AudioClips.SingleAsync(
            value => value.Id == audioId,
            TestContext.Current.CancellationToken);
        db.AudioClips.Remove(audio);

        var action = async () => await db.SaveChangesAsync(
            TestContext.Current.CancellationToken);
        await action.Should().ThrowAsync<DbUpdateException>();
    }

    /// <summary>
    /// 验证删除 Draft 会级联私有子项但保留被引用的 AudioClip。
    /// </summary>
    private static async Task VerifyCascadeAndAudioRestrictAsync(
        DbContextOptions<ApplicationDbContext> options,
        Guid audioId)
    {
        Guid draftId;
        await using (var db = new ApplicationDbContext(options))
        {
            var userId = await db.Users.Select(value => value.Id)
                .FirstAsync(TestContext.Current.CancellationToken);
            var service = CreateService(db);
            var draft = await service.CreateDraftAsync(
                userId,
                CreateCompleteRequest(audioId, secondAudioId: null) with
                {
                    Headword = "draft-only"
                },
                TestContext.Current.CancellationToken);
            draftId = draft.Id;
            await service.DeleteAsync(
                draft.Id,
                userId,
                TestContext.Current.CancellationToken);
        }

        await using var verificationDb = new ApplicationDbContext(options);
        (await verificationDb.Words.AnyAsync(
            value => value.Id == draftId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verificationDb.WordSenses.AnyAsync(
            value => value.WordId == draftId,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await verificationDb.AudioClips.AnyAsync(
            value => value.Id == audioId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    /// <summary>
    /// 创建并保存测试 editor 用户。
    /// </summary>
    private static async Task<User> CreateUserAsync(ApplicationDbContext db)
    {
        var user = new User
        {
            Username = $"editor-{Guid.NewGuid():N}",
            Email = $"editor-{Guid.NewGuid():N}@example.test",
            PasswordHash = "not-used",
            Role = UserRole.Editor
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user;
    }

    /// <summary>
    /// 创建并保存处理就绪、已发布的测试音频。
    /// </summary>
    private static async Task<AudioClip> CreateAudioAsync(
        ApplicationDbContext db,
        User owner,
        AudioClipKind kind,
        string languageTag)
    {
        var source = new MediaResource
        {
            UploaderId = owner.Id,
            Uploader = owner,
            ObjectName = $"word-tests/{Guid.NewGuid():N}.mp3",
            OriginalName = "audio.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var audio = new AudioClip
        {
            OwnerId = owner.Id,
            Owner = owner,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = "Pronunciation",
            LanguageTag = languageTag,
            Kind = kind,
            ProcessingStatus = AudioProcessingStatus.Ready,
            PublicationStatus = AudioPublicationStatus.Published
        };
        db.AudioClips.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return audio;
    }

    /// <summary>
    /// 创建真实数据库测试使用的词条服务。
    /// </summary>
    private static WordService CreateService(ApplicationDbContext db)
        => new(
            db,
            new PostgresDatabaseExceptionClassifier(),
            TimeProvider.System,
            NullLogger<WordService>.Instance);

    /// <summary>
    /// 创建包含一个例句和默认发音的完整请求。
    /// </summary>
    private static CreateWordRequest CreateCompleteRequest(
        Guid audioId,
        Guid? secondAudioId)
    {
        var pronunciations = new List<WordPronunciationInput>
        {
            new()
            {
                AudioClipId = audioId,
                IsDefault = true,
                SortOrder = 0
            }
        };
        if (secondAudioId is { } additionalAudioId)
        {
            pronunciations.Add(new WordPronunciationInput
            {
                AudioClipId = additionalAudioId,
                SortOrder = 1
            });
        }

        return new CreateWordRequest
        {
            Headword = "hello",
            LanguageTag = "en",
            Senses =
            [
                new WordSenseInput
                {
                    PartOfSpeech = PartOfSpeech.Interjection,
                    Definition = "a greeting",
                    DefinitionLanguageTag = "en",
                    SortOrder = 0,
                    Examples =
                    [
                        new ExampleSentenceInput
                        {
                            Sentence = "Hello there.",
                            LanguageTag = "en",
                            Translation = "Bonjour.",
                            TranslationLanguageTag = "fr",
                            SortOrder = 0
                        }
                    ]
                }
            ],
            Pronunciations = pronunciations
        };
    }

    /// <summary>
    /// 从管理详情创建交换两个已有发音默认项和排序的完整更新请求。
    /// </summary>
    private static UpdateWordRequest CreateDefaultSwapRequest(
        EditorWordResponse response,
        Guid firstAudioId,
        Guid secondAudioId)
        => new()
        {
            Headword = response.Headword,
            LanguageTag = response.LanguageTag,
            ConcurrencyStamp = response.ConcurrencyStamp,
            Senses = response.Senses.Select(sense => new WordSenseInput
            {
                Id = sense.Id,
                PartOfSpeech = sense.PartOfSpeech,
                Definition = sense.Definition,
                DefinitionLanguageTag = sense.DefinitionLanguageTag,
                UsageNote = sense.UsageNote,
                SortOrder = sense.SortOrder,
                Examples = sense.Examples.Select(example => new ExampleSentenceInput
                {
                    Id = example.Id,
                    Sentence = example.Sentence,
                    LanguageTag = example.LanguageTag,
                    Translation = example.Translation,
                    TranslationLanguageTag = example.TranslationLanguageTag,
                    AudioClipId = example.AudioClipId,
                    SortOrder = example.SortOrder
                }).ToArray()
            }).ToArray(),
            Pronunciations = response.Pronunciations.Select(pronunciation =>
                new WordPronunciationInput
                {
                    Id = pronunciation.Id,
                    AudioClipId = pronunciation.AudioClipId,
                    AccentTag = pronunciation.AccentTag,
                    Ipa = pronunciation.Ipa,
                    IsDefault = pronunciation.AudioClipId == secondAudioId,
                    SortOrder = pronunciation.AudioClipId == firstAudioId ? 1 : 0
                }).ToArray()
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
