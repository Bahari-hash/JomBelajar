using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using TinyLang.Database;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Interfaces;
using TinyLang.Services;
using TinyLang.Exceptions;

namespace TinyLang.UnitTests;

public sealed class PaperBatchServiceTests
{
    [Fact]
    public async Task PaperServiceShouldPersistDictationAudioAndOrderedBlanks()
    {
        await using var db = CreateDbContext();
        var audio = CreateAudio("lesson-01.mp3", AudioResourceStatus.Ready);
        db.AudioResources.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var service = CreatePaperService(db);

        var response = await service.CreateDraftAsync(Guid.NewGuid(),
            CreatePaperRequest(audio.Id), TestContext.Current.CancellationToken);

        var question = response.Questions.Should().ContainSingle().Subject;
        question.AudioResourceId.Should().Be(audio.Id);
        question.DictationBlanks.Select(value => value.Answer)
            .Should().Equal("hello", "world");
    }

    [Fact]
    public async Task PaperServiceShouldRejectDictationAudioThatIsNotReady()
    {
        await using var db = CreateDbContext();
        var audio = CreateAudio("lesson-01.mp3", AudioResourceStatus.Processing);
        db.AudioResources.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var action = () => CreatePaperService(db).CreateDraftAsync(
            Guid.NewGuid(), CreatePaperRequest(audio.Id),
            TestContext.Current.CancellationToken);

        await action.Should().ThrowAsync<ConflictException>();
        (await db.Papers.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task ValidateShouldResolveReadyAudioAndActiveCategories()
    {
        await using var db = CreateDbContext();
        var category = new PaperCategory
        {
            Name = "日常会话",
            Slug = "daily-conversation",
            IsActive = true
        };
        var audio = CreateAudio("lesson-01.mp3", AudioResourceStatus.Ready);
        db.AddRange(category, audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService(db).ValidateAsync(CreateRequest(),
            TestContext.Current.CancellationToken);

        result.IsValid.Should().BeTrue();
        result.Summary.CategoryReferenceCount.Should().Be(1);
        result.Summary.MatchedCategoryReferenceCount.Should().Be(1);
        result.Summary.AudioReferenceCount.Should().Be(1);
        result.Summary.MatchedAudioReferenceCount.Should().Be(1);
        result.Papers.Single().MatchedCategoryCount.Should().Be(1);
        result.Papers.Single().MatchedAudioCount.Should().Be(1);
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateShouldReturnJsonFieldPathForInvalidDictationBlank()
    {
        await using var db = CreateDbContext();
        db.PaperCategories.Add(new PaperCategory
        {
            Name = "日常会话",
            Slug = "daily-conversation",
            IsActive = true
        });
        db.AudioResources.Add(CreateAudio("lesson-01.mp3", AudioResourceStatus.Ready));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var request = CreateRequest();
        var paper = request.Papers.Single();
        var question = paper.Questions.Single();
        request = request with
        {
            Papers =
            [
                paper with
                {
                    Questions =
                    [
                        question with
                        {
                            Blanks = [new PaperDictationBlankInput
                            {
                                Answer = " ",
                                SortOrder = 0
                            }]
                        }
                    ]
                }
            ]
        };

        var result = await CreateService(db).ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        result.Errors.Should().Contain(error =>
            error.Field == "papers[0].questions[0].blanks[0].answer");
    }

    [Fact]
    public async Task ImportShouldReturnAtomicValidationWhenReferenceChangesDuringSave()
    {
        await using var db = CreateDbContext();
        db.PaperCategories.Add(new PaperCategory
        {
            Name = "日常会话",
            Slug = "daily-conversation",
            IsActive = true
        });
        db.AudioResources.Add(CreateAudio("lesson-01.mp3", AudioResourceStatus.Ready));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var paperService = new Mock<IPaperService>();
        paperService.Setup(value => value.CreateDraftAsync(
                It.IsAny<Guid>(),
                It.IsAny<CreatePaperRequest>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(ConflictException.Create(ErrorCodes.AudioNotReady));

        var result = await new PaperBatchService(
            db,
            paperService.Object,
            new CreatePaperRequestValidator()).ImportAsync(
                Guid.NewGuid(),
                CreateRequest(),
                TestContext.Current.CancellationToken);

        result.Imported.Should().BeNull();
        result.Validation!.IsValid.Should().BeFalse();
        result.Validation.Errors.Should().Contain(error =>
            error.ErrorCode == ErrorCodes.PaperBatchConflict);
        (await db.Papers.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task ImportShouldRejectEntireBatchWhenAnyAudioIsNotReady()
    {
        await using var db = CreateDbContext();
        db.PaperCategories.Add(new PaperCategory
        {
            Name = "日常会话",
            Slug = "daily-conversation",
            IsActive = true
        });
        db.AudioResources.Add(CreateAudio("lesson-01.mp3", AudioResourceStatus.Processing));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService(db).ImportAsync(
            Guid.NewGuid(), CreateRequest(), TestContext.Current.CancellationToken);

        result.Imported.Should().BeNull();
        result.Validation!.Errors.Should().Contain(error =>
            error.ErrorCode == Exceptions.ErrorCodes.PaperBatchAudioNotReady);
        (await db.Papers.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
    }

    [Fact]
    public async Task ImportShouldCreateEveryPaperAsDraftWithOrderedDictationBlanks()
    {
        await using var db = CreateDbContext();
        db.PaperCategories.Add(new PaperCategory
        {
            Name = "日常会话",
            Slug = "daily-conversation",
            IsActive = true
        });
        db.AudioResources.Add(CreateAudio("lesson-01.mp3", AudioResourceStatus.Ready));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);

        var result = await CreateService(db).ImportAsync(
            Guid.NewGuid(), CreateRequest(), TestContext.Current.CancellationToken);

        result.Imported!.ImportedCount.Should().Be(1);
        var paper = await db.Papers.Include(value => value.Questions)
            .ThenInclude(value => value.DictationBlanks)
            .SingleAsync(TestContext.Current.CancellationToken);
        paper.Status.Should().Be(PaperPublicationStatus.Draft);
        paper.Questions.Single().DictationBlanks.OrderBy(value => value.SortOrder)
            .Select(value => value.NormalizedAnswer)
            .Should().Equal("HELLO", "WORLD");
    }

    private static PaperBatchRequest CreateRequest() => new()
    {
        Papers =
        [
            new PaperBatchItemRequest
            {
                Title = "基础听写",
                CategoryNames = ["日常会话"],
                Questions =
                [
                    new PaperBatchQuestionRequest
                    {
                        Type = PaperQuestionType.Dictation,
                        Prompt = "听音频并填写全部内容",
                        Points = 10,
                        AudioFileName = "lesson-01.mp3",
                        Blanks =
                        [
                            new PaperDictationBlankInput { Answer = " hello ", SortOrder = 0 },
                            new PaperDictationBlankInput { Answer = "world", SortOrder = 1 }
                        ]
                    }
                ]
            }
        ]
    };

    private static CreatePaperRequest CreatePaperRequest(Guid audioId) => new()
    {
        Title = "基础听写",
        Questions =
        [
            new PaperQuestionInput
            {
                Type = PaperQuestionType.Dictation,
                Prompt = "听音频并填写全部内容",
                Points = 10,
                AudioResourceId = audioId,
                DictationBlanks =
                [
                    new PaperDictationBlankInput { Answer = " hello ", SortOrder = 0 },
                    new PaperDictationBlankInput { Answer = "world", SortOrder = 1 }
                ]
            }
        ]
    };

    private static PaperBatchService CreateService(ApplicationDbContext db)
    {
        var paperService = CreatePaperService(db);
        return new PaperBatchService(db, paperService, new CreatePaperRequestValidator());
    }

    private static PaperService CreatePaperService(ApplicationDbContext db)
        => new(
            db,
            Mock.Of<IDatabaseExceptionClassifier>(),
            TimeProvider.System,
            NullLogger<PaperService>.Instance);

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    private static AudioResource CreateAudio(string name, AudioResourceStatus status)
    {
        var admin = new User
        {
            Email = $"admin-{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = UserRole.Admin
        };
        var source = new MediaResource
        {
            Uploader = admin,
            UploaderId = admin.Id,
            ObjectName = $"audios/{Guid.NewGuid():N}.mp3",
            OriginalName = name,
            Module = ResourceModule.Audio,
            Status = ResourceStatus.Active,
            Size = 1024,
            Extension = ".mp3",
            ContentType = "audio/mpeg"
        };
        var audio = AudioResource.Create(admin.Id, name, source.Id);
        audio.SourceMediaResource = source;
        audio.CreatedBy = admin;
        audio.LastEditor = admin;
        audio.Status = status;
        return audio;
    }
}
