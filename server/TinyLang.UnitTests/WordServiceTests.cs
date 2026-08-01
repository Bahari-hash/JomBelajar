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
/// 验证词条聚合写入、音频边界、生命周期、并发和用户可见性。
/// </summary>
public sealed class WordServiceTests
{
    private static readonly DateTimeOffset Now = new(
        2026, 7, 29, 4, 0, 0, TimeSpan.Zero);

    /// <summary>
    /// 验证创建会规范化身份字段并一次保存完整嵌套结构和审计人。
    /// </summary>
    [Fact]
    public async Task CreateDraftShouldNormalizeAndPersistCompleteAggregate()
    {
        await using var db = CreateDbContext();
        var pronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en-US");
        var exampleAudio = CreateAudio(AudioClipKind.ExampleSentence, "en");
        db.AudioClips.AddRange(pronunciation, exampleAudio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var adminId = Guid.NewGuid();
        var service = CreateService(db);

        var response = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest(pronunciation.Id, exampleAudio.Id) with
            {
                Headword = "  Cafe\u0301  ",
                LanguageTag = "EN-us"
            },
            TestContext.Current.CancellationToken);

        response.Headword.Should().Be("Caf\u00e9");
        response.LanguageTag.Should().Be("en-us");
        response.CreatedBy.Id.Should().Be(adminId);
        response.LastEditor.Id.Should().Be(adminId);
        response.Senses.Should().ContainSingle();
        response.Senses.Single().Examples.Should().ContainSingle();
        response.Pronunciations.Should().ContainSingle();
        var stored = await db.Words.Include(value => value.Senses)
            .ThenInclude(value => value.Examples)
            .Include(value => value.Pronunciations)
            .SingleAsync(value => value.Id == response.Id, TestContext.Current.CancellationToken);
        stored.NormalizedHeadword.Should().Be("CAF\u00c9");
    }

    /// <summary>
    /// 验证相同语言的规范化词头冲突，而不同语言可以共存。
    /// </summary>
    [Fact]
    public async Task NormalizedHeadwordShouldBeUniqueWithinLanguage()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var adminId = Guid.NewGuid();
        await service.CreateDraftAsync(
            adminId,
            new CreateWordRequest { Headword = " Hello ", LanguageTag = "en" },
            TestContext.Current.CancellationToken);

        var duplicate = async () => await service.CreateDraftAsync(
            adminId,
            new CreateWordRequest { Headword = "hello", LanguageTag = "EN" },
            TestContext.Current.CancellationToken);
        await duplicate.Should().ThrowAsync<ConflictException>();

        var otherLanguage = await service.CreateDraftAsync(
            adminId,
            new CreateWordRequest { Headword = "hello", LanguageTag = "fr" },
            TestContext.Current.CancellationToken);
        otherLanguage.LanguageTag.Should().Be("fr");
    }

    /// <summary>
    /// 验证完整更新保留已有 ID、删除遗漏项、加入新项并可交换默认发音和排序。
    /// </summary>
    [Fact]
    public async Task UpdateShouldSynchronizeCompleteTargetAndPreserveExistingIds()
    {
        await using var db = CreateDbContext();
        var firstPronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en");
        var secondPronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en-GB");
        var exampleAudio = CreateAudio(AudioClipKind.ExampleSentence, "en");
        db.AudioClips.AddRange(firstPronunciation, secondPronunciation, exampleAudio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(firstPronunciation.Id, exampleAudio.Id) with
            {
                Pronunciations =
                [
                    new WordPronunciationInput
                    {
                        AudioClipId = firstPronunciation.Id,
                        IsDefault = true,
                        SortOrder = 0
                    },
                    new WordPronunciationInput
                    {
                        AudioClipId = secondPronunciation.Id,
                        SortOrder = 1
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        var existingSense = created.Senses.Single();
        var existingExample = existingSense.Examples.Single();
        var firstAssociation = created.Pronunciations.Single(value =>
            value.AudioClipId == firstPronunciation.Id);
        var secondAssociation = created.Pronunciations.Single(value =>
            value.AudioClipId == secondPronunciation.Id);

        var updated = await service.UpdateAsync(
            created.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "hello",
                LanguageTag = "en",
                ConcurrencyStamp = created.ConcurrencyStamp,
                Senses =
                [
                    CreateSenseInput(sortOrder: 1, exampleAudio.Id) with
                    {
                        Id = existingSense.Id,
                        Definition = "an updated greeting",
                        Examples =
                        [
                            CreateExampleInput(0, exampleAudio.Id) with
                            {
                                Id = existingExample.Id,
                                Sentence = "Hello again."
                            }
                        ]
                    },
                    CreateSenseInput(sortOrder: 0, audioClipId: null) with
                    {
                        PartOfSpeech = PartOfSpeech.Interjection,
                        Definition = "used to attract attention"
                    }
                ],
                Pronunciations =
                [
                    new WordPronunciationInput
                    {
                        Id = firstAssociation.Id,
                        AudioClipId = firstPronunciation.Id,
                        SortOrder = 1
                    },
                    new WordPronunciationInput
                    {
                        Id = secondAssociation.Id,
                        AudioClipId = secondPronunciation.Id,
                        IsDefault = true,
                        SortOrder = 0
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        updated.Senses.Should().HaveCount(2);
        updated.Senses.Should().Contain(value =>
            value.Id == existingSense.Id && value.Definition == "an updated greeting");
        updated.Senses.Single(value => value.Id == existingSense.Id)
            .Examples.Single().Id.Should().Be(existingExample.Id);
        updated.Pronunciations.Single(value => value.Id == secondAssociation.Id)
            .IsDefault.Should().BeTrue();
        updated.Pronunciations.Single(value => value.Id == firstAssociation.Id)
            .IsDefault.Should().BeFalse();
        updated.ConcurrencyStamp.Should().NotBe(created.ConcurrencyStamp);
    }

    /// <summary>
    /// 验证跨聚合子项标识和过期并发标识均产生稳定失败。
    /// </summary>
    [Fact]
    public async Task UpdateShouldRejectForeignChildAndStaleConcurrencyStamp()
    {
        await using var db = CreateDbContext();
        var audio = CreateAudio(AudioClipKind.WordPronunciation, "en");
        db.AudioClips.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var first = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(audio.Id, null),
            TestContext.Current.CancellationToken);
        var second = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(audio.Id, null) with { Headword = "world" },
            TestContext.Current.CancellationToken);

        var foreignChild = async () => await service.UpdateAsync(
            first.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "hello",
                LanguageTag = "en",
                ConcurrencyStamp = first.ConcurrencyStamp,
                Senses =
                [
                    CreateSenseInput(0, null) with { Id = second.Senses.Single().Id }
                ],
                Pronunciations = []
            },
            TestContext.Current.CancellationToken);
        await foreignChild.Should().ThrowAsync<RequestValidationException>();

        var stale = async () => await service.UpdateAsync(
            first.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "hello",
                LanguageTag = "en",
                ConcurrencyStamp = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken);
        await stale.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证缺失、不可用、用途错误和语言不兼容的音频均不能关联。
    /// </summary>
    [Fact]
    public async Task AudioAssociationShouldEnforceExistenceStatusKindAndLanguage()
    {
        await using var db = CreateDbContext();
        var unavailable = CreateAudio(
            AudioClipKind.WordPronunciation,
            "en",
            AudioProcessingStatus.Processing);
        var wrongKind = CreateAudio(AudioClipKind.ExampleSentence, "en");
        var wrongLanguage = CreateAudio(AudioClipKind.WordPronunciation, "fr");
        db.AudioClips.AddRange(unavailable, wrongKind, wrongLanguage);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var missing = async () => await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(Guid.NewGuid(), null),
            TestContext.Current.CancellationToken);
        var invalidStatus = async () => await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(unavailable.Id, null),
            TestContext.Current.CancellationToken);
        var invalidKind = async () => await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(wrongKind.Id, null),
            TestContext.Current.CancellationToken);
        var invalidLanguage = async () => await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(wrongLanguage.Id, null),
            TestContext.Current.CancellationToken);

        await missing.Should().ThrowAsync<NotFoundException>();
        await invalidStatus.Should().ThrowAsync<ConflictException>();
        await invalidKind.Should().ThrowAsync<ConflictException>();
        await invalidLanguage.Should().ThrowAsync<ConflictException>();
    }

    /// <summary>
    /// 验证发布、重复发布、下架和重新发布保持幂等及首次发布时间。
    /// </summary>
    [Fact]
    public async Task PublicationLifecycleShouldBeIdempotentAndPreserveFirstPublishedAt()
    {
        await using var db = CreateDbContext();
        var pronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en");
        db.AudioClips.Add(pronunciation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var adminId = Guid.NewGuid();
        var created = await service.CreateDraftAsync(
            adminId,
            CreateCompleteRequest(pronunciation.Id, null),
            TestContext.Current.CancellationToken);

        var published = await service.PublishAsync(
            created.Id,
            adminId,
            new WordMutationRequest { ConcurrencyStamp = created.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var repeatedPublish = await service.PublishAsync(
            created.Id,
            adminId,
            new WordMutationRequest { ConcurrencyStamp = published.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var userDetail = await service.GetUserByIdAsync(
            created.Id, TestContext.Current.CancellationToken);
        var unpublished = await service.UnpublishAsync(
            created.Id,
            adminId,
            new WordMutationRequest
            {
                ConcurrencyStamp = repeatedPublish.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var repeatedUnpublish = await service.UnpublishAsync(
            created.Id,
            adminId,
            new WordMutationRequest { ConcurrencyStamp = unpublished.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var republished = await service.PublishAsync(
            created.Id,
            adminId,
            new WordMutationRequest
            {
                ConcurrencyStamp = repeatedUnpublish.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);

        published.Status.Should().Be(WordPublicationStatus.Published);
        published.PublishedAt.Should().Be(Now);
        repeatedPublish.ConcurrencyStamp.Should().Be(published.ConcurrencyStamp);
        userDetail.Id.Should().Be(created.Id);
        unpublished.PublishedAt.Should().Be(Now);
        repeatedUnpublish.ConcurrencyStamp.Should().Be(unpublished.ConcurrencyStamp);
        republished.PublishedAt.Should().Be(Now);
    }

    /// <summary>
    /// 验证幂等发布在客户端提交过期并发戳时不会吞掉冲突。
    /// </summary>
    [Fact]
    public async Task PublicationShouldRejectStaleStampBeforeIdempotentStateCheck()
    {
        await using var db = CreateDbContext();
        var pronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en");
        db.AudioClips.Add(pronunciation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(pronunciation.Id, null),
            TestContext.Current.CancellationToken);
        var published = await service.PublishAsync(
            created.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = created.ConcurrencyStamp },
            TestContext.Current.CancellationToken);

        var action = () => service.PublishAsync(
            created.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = Guid.NewGuid() },
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordConcurrencyConflict);
        published.Status.Should().Be(WordPublicationStatus.Published);
    }

    /// <summary>
    /// 验证归档是不可恢复终态，并从管理员默认列表和用户可见范围排除。
    /// </summary>
    [Fact]
    public async Task ArchiveShouldBeTerminalAndExcludedFromDefaultQueries()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            Guid.NewGuid(),
            new CreateWordRequest { Headword = "archivable", LanguageTag = "en" },
            TestContext.Current.CancellationToken);

        var archived = await service.ArchiveAsync(
            created.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = created.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var defaultList = await service.GetAdminListAsync(
            new AdminWordListRequest(),
            TestContext.Current.CancellationToken);
        var archivedList = await service.GetAdminListAsync(
            new AdminWordListRequest { Status = WordPublicationStatus.Archived },
            TestContext.Current.CancellationToken);
        var userList = await service.GetUserListAsync(
            new WordListRequest(),
            TestContext.Current.CancellationToken);
        var userDetail = () => service.GetUserByIdAsync(
            created.Id,
            TestContext.Current.CancellationToken);
        var repeatArchive = () => service.ArchiveAsync(
            created.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = archived.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        var update = () => service.UpdateAsync(
            created.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "changed",
                LanguageTag = "en",
                ConcurrencyStamp = archived.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        var staleUpdate = () => service.UpdateAsync(
            created.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "changed",
                LanguageTag = "en",
                ConcurrencyStamp = Guid.NewGuid()
            },
            TestContext.Current.CancellationToken);

        archived.Status.Should().Be(WordPublicationStatus.Archived);
        archived.ArchivedAt.Should().Be(Now);
        defaultList.Items.Should().BeEmpty();
        archivedList.Items.Should().ContainSingle(value => value.Id == created.Id);
        userList.Items.Should().BeEmpty();
        await userDetail.Should().ThrowAsync<NotFoundException>();
        await repeatArchive.Should().ThrowAsync<ConflictException>();
        await update.Should().ThrowAsync<ConflictException>();
        var staleException = await staleUpdate.Should().ThrowAsync<ConflictException>();
        staleException.Which.ErrorCode.Should().Be(ErrorCodes.WordConcurrencyConflict);
    }

    /// <summary>
    /// 验证管理员列表可以按词性和定义筛选，并返回首要释义摘要。
    /// </summary>
    [Fact]
    public async Task AdminListShouldFilterByPartOfSpeechAndDefinition()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await service.CreateDraftAsync(
            Guid.NewGuid(),
            new CreateWordRequest
            {
                Headword = "noun-word",
                LanguageTag = "en",
                Senses =
                [
                    new WordSenseInput
                    {
                        PartOfSpeech = PartOfSpeech.Noun,
                        Definition = "A meaningful noun",
                        DefinitionLanguageTag = "en",
                        SortOrder = 0
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        await service.CreateDraftAsync(
            Guid.NewGuid(),
            new CreateWordRequest
            {
                Headword = "verb-word",
                LanguageTag = "en",
                Senses =
                [
                    new WordSenseInput
                    {
                        PartOfSpeech = PartOfSpeech.Verb,
                        Definition = "An action",
                        DefinitionLanguageTag = "en",
                        SortOrder = 0
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var byPartOfSpeech = await service.GetAdminListAsync(
            new AdminWordListRequest { PartOfSpeech = PartOfSpeech.Noun },
            TestContext.Current.CancellationToken);
        var byDefinition = await service.GetAdminListAsync(
            new AdminWordListRequest { Definition = "MEANINGFUL" },
            TestContext.Current.CancellationToken);

        byPartOfSpeech.Items.Should().ContainSingle(value =>
            value.PrimaryPartOfSpeech == PartOfSpeech.Noun &&
            value.PrimaryDefinition == "A meaningful noun");
        byDefinition.Items.Should().ContainSingle(value =>
            value.Headword == "noun-word" && value.SenseCount == 1);
    }

    /// <summary>
    /// 验证批量校验只返回规范化预览和稳定嵌套字段路径，不写入数据库。
    /// </summary>
    [Fact]
    public async Task BatchValidationShouldNormalizeWithoutWriting()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new BatchWordRequest
        {
            Rows =
            [
                new BatchWordRowRequest
                {
                    LanguageTag = "EN-us",
                    Headword = "  Hello  ",
                    Senses =
                    [
                        new BatchWordSenseInput
                        {
                            PartOfSpeech = PartOfSpeech.Noun,
                            Definition = "  a greeting ",
                            DefinitionLanguageTag = "EN",
                            SortOrder = 0
                        }
                    ]
                },
                new BatchWordRowRequest
                {
                    LanguageTag = "en",
                    Headword = "invalid",
                    Senses =
                    [
                        new BatchWordSenseInput
                        {
                            PartOfSpeech = PartOfSpeech.Noun,
                            Definition = string.Empty,
                            DefinitionLanguageTag = "en",
                            SortOrder = 0
                        }
                    ]
                }
            ]
        };

        var response = await service.ValidateBatchAsync(
            request,
            TestContext.Current.CancellationToken);
        var normalized = response.Rows[0].Normalized
            ?? throw new InvalidOperationException("Expected normalized preview.");

        response.IsValid.Should().BeFalse();
        normalized.LanguageTag.Should().Be("en-us");
        normalized.Headword.Should().Be("Hello");
        normalized.Senses.Single().Definition.Should().Be("a greeting");
        response.Rows[1].Errors.Should().Contain(error =>
            error.Field == "senses[0].definition" &&
            error.ErrorCodes.Contains(ErrorCodes.WordDefinitionRequired));
        (await db.Words.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// 验证批量导入一次创建全部草稿，并按输入行返回稳定映射。
    /// </summary>
    [Fact]
    public async Task BatchImportShouldCreateDraftsAtomically()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var adminId = Guid.NewGuid();
        var response = await service.ImportBatchAsync(
            adminId,
            new BatchWordRequest
            {
                Rows =
                [
                    new BatchWordRowRequest { LanguageTag = "en", Headword = "alpha" },
                    new BatchWordRowRequest { LanguageTag = "en", Headword = "beta" }
                ]
            },
            TestContext.Current.CancellationToken);

        response.CreatedCount.Should().Be(2);
        response.Items.Select(value => value.RowIndex).Should().Equal(0, 1);
        (await db.Words.CountAsync(TestContext.Current.CancellationToken)).Should().Be(2);
        (await db.Words.AllAsync(value =>
                value.Status == WordPublicationStatus.Draft &&
                value.CreatedById == adminId,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    /// <summary>
    /// 验证批量重复或任一行无效时整个导入不会产生部分写入。
    /// </summary>
    [Fact]
    public async Task BatchImportShouldRollbackWhenAnyRowFails()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var request = new BatchWordRequest
        {
            Rows =
            [
                new BatchWordRowRequest { LanguageTag = "en", Headword = "duplicate" },
                new BatchWordRowRequest { LanguageTag = "EN", Headword = " duplicate " }
            ]
        };

        var action = () => service.ImportBatchAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<RequestValidationException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordBatchValidationFailed);
        (await db.Words.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// 验证批量音频引用错误定位到嵌套字段且不会创建词条。
    /// </summary>
    [Fact]
    public async Task BatchValidationShouldReportUnavailableAudio()
    {
        await using var db = CreateDbContext();
        var unavailable = CreateAudio(
            AudioClipKind.WordPronunciation,
            "en",
            AudioProcessingStatus.Processing);
        db.AudioClips.Add(unavailable);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var response = await service.ValidateBatchAsync(
            new BatchWordRequest
            {
                Rows =
                [
                    new BatchWordRowRequest
                    {
                        LanguageTag = "en",
                        Headword = "audio-word",
                        Pronunciations =
                        [
                            new BatchWordPronunciationInput
                            {
                                AudioClipId = unavailable.Id,
                                IsDefault = true,
                                SortOrder = 0
                            }
                        ]
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Rows.Single().Errors.Should().Contain(error =>
            error.Field == "pronunciations[0].audioClipId" &&
            error.ErrorCodes.Contains(ErrorCodes.WordAudioUnavailable));
        (await db.Words.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    /// <summary>
    /// 验证关联音频后续失效时用户查询隐藏词条而管理员仍可查看。
    /// </summary>
    [Fact]
    public async Task UnavailableLinkedAudioShouldHidePublishedWordFromUsers()
    {
        await using var db = CreateDbContext();
        var pronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en");
        db.AudioClips.Add(pronunciation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(pronunciation.Id, null),
            TestContext.Current.CancellationToken);
        await service.PublishAsync(
            created.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = created.ConcurrencyStamp },
            TestContext.Current.CancellationToken);
        pronunciation.PublicationStatus = AudioPublicationStatus.Unpublished;
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var list = await service.GetUserListAsync(
            new WordListRequest(), TestContext.Current.CancellationToken);
        var detail = async () => await service.GetUserByIdAsync(
            created.Id, TestContext.Current.CancellationToken);
        var adminDetail = await service.GetAdminByIdAsync(
            created.Id, TestContext.Current.CancellationToken);

        list.Items.Should().BeEmpty();
        await detail.Should().ThrowAsync<NotFoundException>();
        adminDetail.Status.Should().Be(WordPublicationStatus.Published);
    }

    /// <summary>
    /// 验证 Published 不能直接删除，而 Draft 删除不会删除关联 AudioClip。
    /// </summary>
    [Fact]
    public async Task DeleteShouldProtectPublishedWordAndKeepAudioClip()
    {
        await using var db = CreateDbContext();
        var pronunciation = CreateAudio(AudioClipKind.WordPronunciation, "en");
        db.AudioClips.Add(pronunciation);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var draft = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(pronunciation.Id, null),
            TestContext.Current.CancellationToken);
        var published = await service.CreateDraftAsync(
            Guid.NewGuid(),
            CreateCompleteRequest(pronunciation.Id, null) with { Headword = "world" },
            TestContext.Current.CancellationToken);
        var publishedResponse = await service.PublishAsync(
            published.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = published.ConcurrencyStamp },
            TestContext.Current.CancellationToken);

        var deletePublished = async () => await service.DeleteAsync(
            published.Id,
            Guid.NewGuid(),
            new WordMutationRequest
            {
                ConcurrencyStamp = publishedResponse.ConcurrencyStamp
            },
            TestContext.Current.CancellationToken);
        await deletePublished.Should().ThrowAsync<ConflictException>();
        await service.DeleteAsync(
            draft.Id,
            Guid.NewGuid(),
            new WordMutationRequest { ConcurrencyStamp = draft.ConcurrencyStamp },
            TestContext.Current.CancellationToken);

        (await db.Words.AnyAsync(
            value => value.Id == draft.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await db.AudioClips.AnyAsync(
            value => value.Id == pronunciation.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    /// <summary>
    /// 创建使用隔离 InMemory database 的应用上下文。
    /// </summary>
    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    /// <summary>
    /// 创建使用固定时间和测试替身依赖的词条服务。
    /// </summary>
    private static WordService CreateService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            new TestTimeProvider(Now),
            Mock.Of<ILogger<WordService>>());

    /// <summary>
    /// 创建处理就绪且默认已发布的音频及其源资源。
    /// </summary>
    private static AudioClip CreateAudio(
        AudioClipKind kind,
        string languageTag,
        AudioProcessingStatus processingStatus = AudioProcessingStatus.Ready,
        AudioPublicationStatus publicationStatus = AudioPublicationStatus.Published)
    {
        var source = new MediaResource
        {
            UploaderId = Guid.NewGuid(),
            ObjectName = $"audios/source/{Guid.NewGuid():N}",
            OriginalName = "audio.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        return new AudioClip
        {
            CreatedById = source.UploaderId,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = "Audio",
            LanguageTag = languageTag,
            Kind = kind,
            ProcessingStatus = processingStatus,
            PublicationStatus = publicationStatus
        };
    }

    /// <summary>
    /// 创建包含一个释义、例句和默认发音的完整词条请求。
    /// </summary>
    private static CreateWordRequest CreateCompleteRequest(
        Guid pronunciationAudioId,
        Guid? exampleAudioId)
        => new()
        {
            Headword = "hello",
            LanguageTag = "en",
            Senses = [CreateSenseInput(0, exampleAudioId)],
            Pronunciations =
            [
                new WordPronunciationInput
                {
                    AudioClipId = pronunciationAudioId,
                    IsDefault = true,
                    SortOrder = 0
                }
            ]
        };

    /// <summary>
    /// 创建指定排序和可选音频的有效释义输入。
    /// </summary>
    private static WordSenseInput CreateSenseInput(int sortOrder, Guid? audioClipId)
        => new()
        {
            PartOfSpeech = PartOfSpeech.Noun,
            Definition = "a greeting",
            DefinitionLanguageTag = "en",
            SortOrder = sortOrder,
            Examples = [CreateExampleInput(0, audioClipId)]
        };

    /// <summary>
    /// 创建指定排序和可选音频的有效例句输入。
    /// </summary>
    private static ExampleSentenceInput CreateExampleInput(
        int sortOrder,
        Guid? audioClipId)
        => new()
        {
            Sentence = "Hello there.",
            LanguageTag = "en",
            Translation = "你好。",
            TranslationLanguageTag = "zh-CN",
            AudioClipId = audioClipId,
            SortOrder = sortOrder
        };
}
