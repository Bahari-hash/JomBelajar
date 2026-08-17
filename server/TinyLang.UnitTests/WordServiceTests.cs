using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Infrastructure;
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
    public async Task DeleteShouldRemoveWordAndPrivateChildren()
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
            TestContext.Current.CancellationToken)).Should().BeFalse();
        (await db.WordSenses.AnyAsync(value => value.WordId == created.Id,
            TestContext.Current.CancellationToken)).Should().BeFalse();
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
