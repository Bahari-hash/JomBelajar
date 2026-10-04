using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Npgsql;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
using TinyLang.Interfaces;
using TinyLang.Services;

namespace TinyLang.UnitTests;

/// <summary>
/// 验证词条聚合写入、共享音频、并发和立即可见性。
/// </summary>
public sealed class WordServiceTests
{
    [Theory]
    [InlineData(AudioResourceStatus.Uploading)]
    [InlineData(AudioResourceStatus.Failed)]
    [InlineData(AudioResourceStatus.Ready)]
    public async Task CreateShouldNormalizePersistAndAcceptAnyAudioStatus(
        AudioResourceStatus status)
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var audio = CreateAudio(admin, status);
        db.AddRange(admin, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);

        var response = await service.CreateAsync(
            admin.Id,
            CreateRequest(audio.Id) with { Headword = "  Cafe\u0301  " },
            TestContext.Current.CancellationToken);

        response.Headword.Should().Be("Caf\u00e9");
        response.Audio.Should().NotBeNull();
        response.Audio!.Id.Should().Be(audio.Id);
        response.Audio.Status.Should().Be(status);
        response.Senses.Should().ContainSingle();
        response.Senses.Single().Examples.Should().BeEmpty();
        var stored = await db.Words.Include(value => value.Senses)
            .SingleAsync(value => value.Id == response.Id,
                TestContext.Current.CancellationToken);
        stored.NormalizedHeadword.Should().Be("CAF\u00c9");
        stored.AudioResourceId.Should().Be(audio.Id);
    }

    [Fact]
    public async Task CreatedWordWithoutAudioShouldBeImmediatelyVisible()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            TestContext.Current.CancellationToken);
        var detail = await service.GetUserByIdAsync(
            created.Id,
            TestContext.Current.CancellationToken);
        var list = await service.GetUserListAsync(
            new WordListRequest(),
            TestContext.Current.CancellationToken);

        detail.AudioResourceId.Should().BeNull();
        detail.Senses.Single().Examples.Should().BeEmpty();
        list.Items.Should().ContainSingle(value => value.Id == created.Id);
    }

    [Fact]
    public async Task CreateShouldAllowExampleWithoutAudio()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var created = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(includeExample: true),
            TestContext.Current.CancellationToken);

        created.Senses.Single().Examples.Single().Audio.Should().BeNull();
        (await db.ExampleSentences.SingleAsync(
            TestContext.Current.CancellationToken)).AudioResourceId.Should().BeNull();
    }

    [Fact]
    public async Task CreateShouldAllowRepeatedUploadingExampleAudioAndProjectDetails()
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var audio = CreateAudio(admin, AudioResourceStatus.Uploading);
        audio.DurationSeconds = 2.25;
        audio.LastFailureCode = "UploadPending";
        db.AddRange(admin, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var request = CreateRequest() with
        {
            Senses =
            [
                CreateSense(0) with
                {
                    Examples =
                    [
                        CreateExample(0) with { AudioResourceId = audio.Id },
                        CreateExample(1) with { AudioResourceId = audio.Id }
                    ]
                }
            ]
        };

        var created = await service.CreateAsync(
            admin.Id,
            request,
            TestContext.Current.CancellationToken);
        var user = await service.GetUserByIdAsync(
            created.Id,
            TestContext.Current.CancellationToken);

        created.Senses.Single().Examples.Should().OnlyContain(example =>
            example.Audio != null &&
            example.Audio.Id == audio.Id &&
            example.Audio.Name == "word.mp3" &&
            example.Audio.Status == AudioResourceStatus.Uploading &&
            example.Audio.DurationSeconds == 2.25 &&
            example.Audio.LastFailureCode == "UploadPending");
        user.Senses.Single().Examples.Should().OnlyContain(example =>
            example.AudioResourceId == audio.Id);
        (await db.ExampleSentences.CountAsync(example =>
            example.AudioResourceId == audio.Id,
            TestContext.Current.CancellationToken)).Should().Be(2);
    }

    [Fact]
    public async Task CreateShouldRejectMissingExampleAudioResource()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var missingAudioId = Guid.NewGuid();
        var request = CreateRequest() with
        {
            Senses =
            [
                CreateSense(0) with
                {
                    Examples =
                    [CreateExample(0) with { AudioResourceId = missingAudioId }]
                }
            ]
        };

        var action = () => service.CreateAsync(
            Guid.NewGuid(),
            request,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordExampleAudioInvalid);
    }

    [Fact]
    public async Task UpdateShouldPreserveReplaceAndClearExampleAudio()
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var originalAudio = CreateAudio(admin, AudioResourceStatus.Ready);
        var replacementAudio = CreateAudio(admin, AudioResourceStatus.Failed);
        db.AddRange(admin, originalAudio, replacementAudio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateAsync(
            admin.Id,
            CreateRequest() with
            {
                Senses =
                [
                    CreateSense(0) with
                    {
                        Examples =
                        [CreateExample(0) with { AudioResourceId = originalAudio.Id }]
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        var sense = created.Senses.Single();
        var example = sense.Examples.Single();

        var preserved = await service.UpdateAsync(
            created.Id,
            admin.Id,
            CreateUpdateRequest(created, sense.Id, example.Id, originalAudio.Id),
            TestContext.Current.CancellationToken);
        var replaced = await service.UpdateAsync(
            created.Id,
            admin.Id,
            CreateUpdateRequest(
                preserved,
                sense.Id,
                example.Id,
                replacementAudio.Id),
            TestContext.Current.CancellationToken);
        var cleared = await service.UpdateAsync(
            created.Id,
            admin.Id,
            CreateUpdateRequest(replaced, sense.Id, example.Id, null),
            TestContext.Current.CancellationToken);

        preserved.Senses.Single().Examples.Single().Audio!.Id
            .Should().Be(originalAudio.Id);
        replaced.Senses.Single().Examples.Single().Audio!.Id
            .Should().Be(replacementAudio.Id);
        cleared.Senses.Single().Examples.Single().Audio.Should().BeNull();
        (await db.ExampleSentences.SingleAsync(
            TestContext.Current.CancellationToken)).AudioResourceId.Should().BeNull();
    }

    [Fact]
    public async Task RemovingExampleShouldNotDeleteSharedAudio()
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var audio = CreateAudio(admin, AudioResourceStatus.Ready);
        db.AddRange(admin, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateAsync(
            admin.Id,
            CreateRequest() with
            {
                Senses =
                [
                    CreateSense(0) with
                    {
                        Examples = [CreateExample(0) with { AudioResourceId = audio.Id }]
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        var sense = created.Senses.Single();

        await service.UpdateAsync(
            created.Id,
            admin.Id,
            new UpdateWordRequest
            {
                Headword = created.Headword,
                ConcurrencyStamp = created.ConcurrencyStamp,
                Senses = [CreateSense(0) with { Id = sense.Id, Examples = [] }]
            },
            TestContext.Current.CancellationToken);

        (await db.ExampleSentences.CountAsync(
            TestContext.Current.CancellationToken)).Should().Be(0);
        (await db.AudioResources.AnyAsync(value => value.Id == audio.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
    }

    [Fact]
    public async Task CreateShouldMapExampleAudioForeignKeyRaceToStableError()
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var audio = CreateAudio(admin, AudioResourceStatus.Ready);
        db.AddRange(admin, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var failure = CreateForeignKeyFailure(
            "FK_example_sentences_audio_resources_AudioResourceId");
        var context = CreateFailingContext(db, failure);
        var service = new WordService(
            context.Object,
            new PostgresDatabaseExceptionClassifier(),
            NullLogger<WordService>.Instance);
        var request = CreateRequest() with
        {
            Senses =
            [
                CreateSense(0) with
                {
                    Examples = [CreateExample(0) with { AudioResourceId = audio.Id }]
                }
            ]
        };

        var action = () => service.CreateAsync(
            admin.Id,
            request,
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordExampleAudioInvalid);
    }

    [Fact]
    public async Task CreateShouldNotMapUnrelatedForeignKeyFailureToExampleAudioError()
    {
        await using var db = CreateDbContext();
        var failure = CreateForeignKeyFailure("FK_other_table_other_principal_OtherId");
        var context = CreateFailingContext(db, failure);
        var service = new WordService(
            context.Object,
            new PostgresDatabaseExceptionClassifier(),
            NullLogger<WordService>.Instance);

        var action = () => service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<DbUpdateException>()
            .Where(exception => ReferenceEquals(exception, failure));
    }

    [Fact]
    public async Task CreateShouldRejectMissingAudioResource()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);

        var action = () => service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(Guid.NewGuid()),
            TestContext.Current.CancellationToken);

        var exception = await action.Should().ThrowAsync<NotFoundException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordAudioInvalid);
    }

    [Fact]
    public async Task UpdateShouldSynchronizeChildrenAndClearAudio()
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var audio = CreateAudio(admin, AudioResourceStatus.Ready);
        db.AddRange(admin, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        var created = await service.CreateAsync(
            admin.Id,
            CreateRequest(audio.Id, includeExample: true),
            TestContext.Current.CancellationToken);
        var existingSense = created.Senses.Single();
        var existingExample = existingSense.Examples.Single();

        var updated = await service.UpdateAsync(
            created.Id,
            admin.Id,
            new UpdateWordRequest
            {
                Headword = "hello",
                AudioResourceId = null,
                ConcurrencyStamp = created.ConcurrencyStamp,
                Senses =
                [
                    CreateSense(1) with
                    {
                        Id = existingSense.Id,
                        Definition = "an updated greeting",
                        Examples =
                        [
                            CreateExample(0) with
                            {
                                Id = existingExample.Id,
                                Sentence = "Hello again."
                            }
                        ]
                    },
                    CreateSense(0) with
                    {
                        PartOfSpeech = PartOfSpeech.Interjection,
                        Definition = "used to attract attention"
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        updated.Audio.Should().BeNull();
        updated.Senses.Should().HaveCount(2);
        updated.Senses.Single(value => value.Id == existingSense.Id)
            .Examples.Single().Id.Should().Be(existingExample.Id);
        updated.ConcurrencyStamp.Should().NotBe(created.ConcurrencyStamp);
    }

    [Fact]
    public async Task UpdateShouldRejectForeignChildAndStaleStamp()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var first = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(),
            TestContext.Current.CancellationToken);
        var second = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest() with { Headword = "world" },
            TestContext.Current.CancellationToken);

        var foreignChild = () => service.UpdateAsync(
            first.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "hello",
                ConcurrencyStamp = first.ConcurrencyStamp,
                Senses = [CreateSense(0) with { Id = second.Senses.Single().Id }]
            },
            TestContext.Current.CancellationToken);
        await foreignChild.Should().ThrowAsync<RequestValidationException>();

        var stale = () => service.UpdateAsync(
            first.Id,
            Guid.NewGuid(),
            new UpdateWordRequest
            {
                Headword = "hello",
                ConcurrencyStamp = Guid.NewGuid(),
                Senses = [CreateSense(0)]
            },
            TestContext.Current.CancellationToken);
        var exception = await stale.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordConcurrencyConflict);
    }

    [Fact]
    public async Task NormalizedHeadwordShouldRemainGloballyUnique()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest() with { Headword = " Hello " },
            TestContext.Current.CancellationToken);

        var duplicate = () => service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest() with { Headword = "hello" },
            TestContext.Current.CancellationToken);

        var exception = await duplicate.Should().ThrowAsync<ConflictException>();
        exception.Which.ErrorCode.Should().Be(ErrorCodes.WordDuplicate);
    }

    [Fact]
    public async Task AdminListShouldFilterAndExposeAudioFlag()
    {
        await using var db = CreateDbContext();
        var admin = CreateAdmin();
        var audio = CreateAudio(admin, AudioResourceStatus.Processing);
        db.AddRange(admin, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreateService(db);
        await service.CreateAsync(
            admin.Id,
            CreateRequest(audio.Id) with
            {
                Headword = "noun-word",
                Senses =
                [
                    CreateSense(0) with
                    {
                        Definition = "A meaningful noun"
                    }
                ]
            },
            TestContext.Current.CancellationToken);
        await service.CreateAsync(
            admin.Id,
            CreateRequest() with
            {
                Headword = "verb-word",
                Senses =
                [
                    CreateSense(0) with
                    {
                        PartOfSpeech = PartOfSpeech.Verb,
                        Definition = "An action"
                    }
                ]
            },
            TestContext.Current.CancellationToken);

        var response = await service.GetAdminListAsync(
            new AdminWordListRequest
            {
                PartOfSpeech = PartOfSpeech.Noun,
                Definition = "MEANINGFUL"
            },
            TestContext.Current.CancellationToken);

        response.Items.Should().ContainSingle(value =>
            value.Headword == "noun-word" && value.HasAudio);
    }

    [Fact]
    public async Task DeleteShouldTombstoneWordAndKeepPrivateChildren()
    {
        await using var db = CreateDbContext();
        var service = CreateService(db);
        var created = await service.CreateAsync(
            Guid.NewGuid(),
            CreateRequest(includeExample: true),
            TestContext.Current.CancellationToken);

        await service.DeleteAsync(
            created.Id,
            Guid.NewGuid(),
            new DeleteWordRequest { ConcurrencyStamp = created.ConcurrencyStamp },
            TestContext.Current.CancellationToken);

        (await db.Words.AnyAsync(value => value.Id == created.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();
        (await db.Words.SingleAsync(value => value.Id == created.Id,
            TestContext.Current.CancellationToken)).IsDeleted.Should().BeTrue();
        (await db.WordSenses.AnyAsync(value => value.WordId == created.Id,
            TestContext.Current.CancellationToken)).Should().BeTrue();

        var replacement = await service.CreateAsync(
            Guid.NewGuid(), CreateRequest(includeExample: true),
            TestContext.Current.CancellationToken);
        replacement.Id.Should().NotBe(created.Id);
        (await db.Words.CountAsync(value => !value.IsDeleted,
            TestContext.Current.CancellationToken)).Should().Be(1);
    }

    private static WordService CreateService(ApplicationDbContext db)
        => new(
            db,
            new PostgresDatabaseExceptionClassifier(),
            NullLogger<WordService>.Instance);

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Mock<IApplicationDbContext> CreateFailingContext(
        ApplicationDbContext db,
        DbUpdateException failure)
    {
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.Words).Returns(db.Words);
        context.SetupGet(value => value.WordSenses).Returns(db.WordSenses);
        context.SetupGet(value => value.ExampleSentences).Returns(db.ExampleSentences);
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.Setup(value => value.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(failure);
        return context;
    }

    private static DbUpdateException CreateForeignKeyFailure(string constraintName)
        => new(
            "write failed",
            new PostgresException(
                "insert or update violates foreign key",
                "ERROR",
                "ERROR",
                PostgresErrorCodes.ForeignKeyViolation,
                schemaName: "public",
                tableName: "example_sentences",
                constraintName: constraintName));

    private static CreateWordRequest CreateRequest(
        Guid? audioResourceId = null,
        bool includeExample = false)
        => new()
        {
            Headword = "hello",
            AudioResourceId = audioResourceId,
            Senses =
            [
                CreateSense(0) with
                {
                    Examples = includeExample ? [CreateExample(0)] : []
                }
            ]
        };

    private static WordSenseInput CreateSense(int sortOrder)
        => new()
        {
            PartOfSpeech = PartOfSpeech.Noun,
            Definition = "a greeting",
            SortOrder = sortOrder,
            Examples = []
        };

    private static ExampleSentenceInput CreateExample(int sortOrder)
        => new()
        {
            Sentence = "Hello there.",
            Translation = "你好。",
            SortOrder = sortOrder
        };

    private static UpdateWordRequest CreateUpdateRequest(
        AdminWordResponse current,
        Guid senseId,
        Guid exampleId,
        Guid? exampleAudioResourceId)
        => new()
        {
            Headword = current.Headword,
            ConcurrencyStamp = current.ConcurrencyStamp,
            Senses =
            [
                CreateSense(0) with
                {
                    Id = senseId,
                    Examples =
                    [
                        CreateExample(0) with
                        {
                            Id = exampleId,
                            AudioResourceId = exampleAudioResourceId
                        }
                    ]
                }
            ]
        };

    private static User CreateAdmin()
        => new()
        {
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };

    private static AudioResource CreateAudio(
        User admin,
        AudioResourceStatus status)
    {
        var source = new MediaResource
        {
            Uploader = admin,
            UploaderId = admin.Id,
            ObjectName = $"audios/{Guid.NewGuid():N}.mp3",
            OriginalName = "word.mp3",
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var audio = AudioResource.Create(admin.Id, "word.mp3", source.Id);
        audio.SourceMediaResource = source;
        audio.CreatedBy = admin;
        audio.LastEditor = admin;
        audio.Status = status;
        audio.DurationSeconds = status == AudioResourceStatus.Ready ? 1.5 : null;
        audio.LastFailureCode = status == AudioResourceStatus.Failed
            ? "TranscodeFailed"
            : null;
        return audio;
    }
}
