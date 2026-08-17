using System.Linq;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
/// 验证批量词条的完整校验、音频名称解析和只读行为。
/// </summary>
public sealed class WordBatchServiceTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ValidateShouldRequireWords(bool useNull)
    {
        await using var db = CreateDbContext();
        var request = new BatchWordRequest
        {
            Words = useNull ? null! : []
        };

        var response = await CreateService(db).ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Errors.Should().ContainSingle(error =>
            error.RowNumber == null &&
            error.Field == "words" &&
            error.ErrorCode == ErrorCodes.WordBatchRequired &&
            error.Message == ErrorCodes.WordBatchRequired.GetMessage());
    }

    [Fact]
    public async Task ValidateShouldRejectMoreThanOneThousandWords()
    {
        await using var db = CreateDbContext();
        var request = new BatchWordRequest
        {
            Words = Enumerable.Range(0, WordConstraints.MaxBatchWordCount + 1)
                .Select(index => CreateRow($"word-{index}"))
                .ToArray()
        };

        var response = await CreateService(db).ValidateAsync(
            request,
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Summary.WordCount.Should().Be(1_001);
        response.Summary.SenseCount.Should().Be(1_001);
        response.Summary.ExampleCount.Should().Be(0);
        response.Errors.Should().ContainSingle(error =>
            error.RowNumber == null &&
            error.Field == "words" &&
            error.ErrorCode == ErrorCodes.WordBatchCountLimit);
    }

    [Fact]
    public async Task ValidateShouldRejectTotalSenseLimit()
    {
        await using var db = CreateDbContext();
        var senses = Enumerable.Range(0, WordConstraints.MaxBatchSenseCount + 1)
            .Select(index => CreateSense(index) with
            {
                Examples = [CreateExample(0)]
            })
            .ToArray();

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = [CreateRow("hello") with { Senses = senses }] },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Summary.SenseCount.Should().Be(WordConstraints.MaxBatchSenseCount + 1);
        response.Summary.ExampleCount.Should().Be(WordConstraints.MaxBatchSenseCount + 1);
        response.Errors.Should().ContainSingle(error =>
            error.Field == "words" &&
            error.ErrorCode == ErrorCodes.WordBatchChildCountLimit);
    }

    [Fact]
    public async Task ValidateShouldRejectTotalExampleLimit()
    {
        await using var db = CreateDbContext();
        var sharedExamples = Enumerable.Range(0, 51)
            .Select(index => CreateExample(index))
            .ToArray();
        var words = Enumerable.Range(0, 1_000)
            .Select(index => CreateRow($"word-{index}") with
            {
                Senses = [CreateSense(0) with { Examples = sharedExamples }]
            })
            .ToArray();

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = words },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Summary.ExampleCount.Should().Be(51_000);
        response.Errors.Should().ContainSingle(error =>
            error.Field == "words" &&
            error.ErrorCode == ErrorCodes.WordBatchChildCountLimit);
    }

    [Fact]
    public async Task ValidateShouldRejectTotalTextLengthLimit()
    {
        await using var db = CreateDbContext();
        var repeatedText = new string('a', 10_001);
        var words = Enumerable.Range(0, 1_000)
            .Select(index => CreateRow($"{index}-{repeatedText}"))
            .ToArray();

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = words },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Errors.Should().ContainSingle(error =>
            error.Field == "words" &&
            error.ErrorCode == ErrorCodes.WordBatchTextLengthLimit);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ValidateShouldIgnoreOmittedOrBlankAudioNames(string? audioName)
    {
        await using var db = CreateDbContext();
        var row = CreateRow("hello") with
        {
            AudioFileName = audioName,
            Senses =
            [
                CreateSense(0) with
                {
                    Examples = [CreateExample(0) with { AudioFileName = audioName }]
                }
            ]
        };

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = [row] },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeTrue();
        response.Summary.WordAudioReferenceCount.Should().Be(0);
        response.Summary.ExampleAudioReferenceCount.Should().Be(0);
        response.Rows.Should().ContainSingle().Which.AudioReferenceCount.Should().Be(0);
    }

    [Theory]
    [InlineData(AudioResourceStatus.Uploading)]
    [InlineData(AudioResourceStatus.Queued)]
    [InlineData(AudioResourceStatus.Processing)]
    [InlineData(AudioResourceStatus.Ready)]
    public async Task ValidateShouldMatchUsableAudioIgnoringCaseAndWhitespace(
        AudioResourceStatus status)
    {
        await using var db = CreateDbContext();
        var audio = CreateAudio("Greeting.MP3", status);
        db.AudioResources.Add(audio);
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var row = CreateRow("  Cafe\u0301  ") with
        {
            AudioFileName = "  GREETING.mp3 ",
            Senses =
            [
                CreateSense(0) with
                {
                    Examples =
                    [CreateExample(0) with { AudioFileName = " greeting.MP3  " }]
                }
            ]
        };

        var build = await CreateService(db).BuildValidationAsync(
            new BatchWordRequest { Words = [row] },
            TestContext.Current.CancellationToken);
        var response = build.Response;

        response.IsValid.Should().BeTrue();
        response.Summary.MatchedAudioReferenceCount.Should().Be(2);
        var preview = response.Rows.Should().ContainSingle().Subject;
        preview.Headword.Should().Be("Caf\u00e9");
        preview.NormalizedHeadword.Should().Be("CAF\u00c9");
        preview.WordAudioName.Should().Be("Greeting.MP3");
        preview.AudioReferenceCount.Should().Be(2);
        preview.MatchedAudioCount.Should().Be(2);
        var prepared = build.Rows.Should().ContainSingle().Subject.Request;
        prepared.AudioResourceId.Should().Be(audio.Id);
        prepared.Senses.Should().ContainSingle()
            .Which.Examples.Should().ContainSingle()
            .Which.AudioResourceId.Should().Be(audio.Id);
    }

    [Fact]
    public async Task ValidateShouldReportFailedAndMissingAudioPerReference()
    {
        await using var db = CreateDbContext();
        db.AudioResources.Add(CreateAudio("failed.mp3", AudioResourceStatus.Failed));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var row = CreateRow("hello") with
        {
            AudioFileName = "failed.mp3",
            Senses =
            [
                CreateSense(0) with
                {
                    Examples =
                    [CreateExample(0) with { AudioFileName = "missing.mp3" }]
                }
            ]
        };

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = [row] },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Errors.Should().Contain(error =>
            error.RowNumber == 1 &&
            error.Field == "words[0].audioFileName" &&
            error.ErrorCode == ErrorCodes.WordBatchAudioFailed);
        response.Errors.Should().Contain(error =>
            error.RowNumber == 1 &&
            error.Field == "words[0].senses[0].examples[0].audioFileName" &&
            error.ErrorCode == ErrorCodes.WordBatchAudioNotFound);
    }

    [Fact]
    public async Task ValidateShouldCountRepeatedAudioReferencesButReadTheSetOnce()
    {
        await using var db = CreateDbContext();
        db.AudioResources.Add(CreateAudio("shared.mp3", AudioResourceStatus.Ready));
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var context = WrapContext(db);
        var row = CreateRow("hello") with
        {
            AudioFileName = "shared.mp3",
            Senses =
            [
                CreateSense(0) with
                {
                    Examples =
                    [
                        CreateExample(0) with { AudioFileName = "SHARED.MP3" },
                        CreateExample(1) with { AudioFileName = " shared.mp3 " }
                    ]
                }
            ]
        };

        var response = await CreateService(context.Object).ValidateAsync(
            new BatchWordRequest { Words = [row] },
            TestContext.Current.CancellationToken);

        response.Summary.WordAudioReferenceCount.Should().Be(1);
        response.Summary.ExampleAudioReferenceCount.Should().Be(2);
        response.Summary.MatchedAudioReferenceCount.Should().Be(3);
        context.VerifyGet(value => value.AudioResources, Times.Once);
    }

    [Fact]
    public async Task ValidateShouldReportEveryBatchAndDatabaseDuplicateRow()
    {
        await using var db = CreateDbContext();
        db.Words.Add(new Word
        {
            Headword = "world",
            NormalizedHeadword = "WORLD"
        });
        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        var context = WrapContext(db);
        var rows = new[]
        {
            CreateRow(" Cafe\u0301 "),
            CreateRow("CAF\u00c9"),
            CreateRow(" world ")
        };

        var response = await CreateService(context.Object).ValidateAsync(
            new BatchWordRequest { Words = rows },
            TestContext.Current.CancellationToken);

        response.Errors.Where(error => error.ErrorCode == ErrorCodes.WordDuplicate)
            .Should().BeEquivalentTo(
            [
                new BatchWordValidationErrorResponse(
                    1,
                    "words[0].headword",
                    ErrorCodes.WordDuplicate,
                    ErrorCodes.WordDuplicate.GetMessage()),
                new BatchWordValidationErrorResponse(
                    2,
                    "words[1].headword",
                    ErrorCodes.WordDuplicate,
                    ErrorCodes.WordDuplicate.GetMessage()),
                new BatchWordValidationErrorResponse(
                    3,
                    "words[2].headword",
                    ErrorCodes.WordDuplicate,
                    ErrorCodes.WordDuplicate.GetMessage())
            ]);
        context.VerifyGet(value => value.Words, Times.Once);
    }

    [Fact]
    public async Task ValidateShouldSafelyReportNullNestedItems()
    {
        await using var db = CreateDbContext();
        var rows = new[]
        {
            CreateRow("first") with { Senses = [null!] },
            CreateRow("second") with
            {
                Senses = [CreateSense(0) with { Examples = [null!] }]
            },
            CreateRow("third") with { Senses = null! },
            CreateRow("fourth") with
            {
                Senses = [CreateSense(0) with { Examples = null! }]
            }
        };

        var action = () => CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = rows },
            TestContext.Current.CancellationToken);

        var response = await action.Should().NotThrowAsync();
        response.Subject.IsValid.Should().BeFalse();
        response.Subject.Errors.Should().Contain(error =>
            error.RowNumber == 1 &&
            error.Field == "words[0].senses[0]" &&
            error.ErrorCode == ErrorCodes.WordChildCollectionInvalid);
        response.Subject.Errors.Should().Contain(error =>
            error.RowNumber == 2 &&
            error.Field == "words[1].senses[0].examples[0]" &&
            error.ErrorCode == ErrorCodes.WordChildCollectionInvalid);
        response.Subject.Errors.Should().ContainSingle(error =>
            error.RowNumber == 3 &&
            error.Field == "words[2].senses" &&
            error.ErrorCode == ErrorCodes.WordChildCollectionInvalid);
        response.Subject.Errors.Should().ContainSingle(error =>
            error.RowNumber == 4 &&
            error.Field == "words[3].senses[0].examples" &&
            error.ErrorCode == ErrorCodes.WordChildCollectionInvalid);
        response.Subject.Errors.Should().NotContain(error =>
            error.RowNumber == 3 &&
            error.ErrorCode == ErrorCodes.WordSenseRequired);
    }

    [Theory]
    [InlineData("noun")]
    [InlineData("UnknownPart")]
    [InlineData("1")]
    public async Task ValidateShouldRejectNonExactPartOfSpeech(string partOfSpeech)
    {
        await using var db = CreateDbContext();
        var row = CreateRow("hello") with
        {
            Senses = [CreateSense(0) with { PartOfSpeech = partOfSpeech }]
        };

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = [row] },
            TestContext.Current.CancellationToken);

        response.Errors.Should().ContainSingle(error =>
            error.RowNumber == 1 &&
            error.Field == "words[0].senses[0].partOfSpeech" &&
            error.ErrorCode == ErrorCodes.WordPartOfSpeechInvalid);
    }

    [Fact]
    public async Task ValidateShouldMapNestedValidatorPathsAndCollectAllErrors()
    {
        await using var db = CreateDbContext();
        var row = new BatchWordRowRequest
        {
            Headword = " ",
            AudioFileName = "missing.mp3",
            Senses =
            [
                new BatchWordSenseInput
                {
                    PartOfSpeech = "noun",
                    Definition = "",
                    SortOrder = -1,
                    Examples =
                    [
                        new BatchExampleSentenceInput
                        {
                            Sentence = "",
                            Translation = "",
                            SortOrder = -1
                        }
                    ]
                }
            ]
        };

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = [row] },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeFalse();
        response.Errors.Should().Contain(error =>
            error.Field == "words[0].headword" &&
            error.ErrorCode == ErrorCodes.WordHeadwordRequired);
        response.Errors.Should().Contain(error =>
            error.Field == "words[0].senses[0].definition" &&
            error.ErrorCode == ErrorCodes.WordDefinitionRequired);
        response.Errors.Should().Contain(error =>
            error.Field == "words[0].senses[0].examples[0].sentence" &&
            error.ErrorCode == ErrorCodes.WordSentenceRequired);
        response.Errors.Should().Contain(error =>
            error.Field == "words[0].senses[0].examples[0].translation" &&
            error.ErrorCode == ErrorCodes.WordTranslationRequired);
        response.Errors.Should().Contain(error =>
            error.Field == "words[0].audioFileName" &&
            error.ErrorCode == ErrorCodes.WordBatchAudioNotFound);
        response.Errors.Select(error => error.RowNumber).Should().OnlyContain(value => value == 1);
    }

    [Fact]
    public async Task ValidateShouldNotTrackOrWriteWords()
    {
        await using var db = CreateDbContext();

        var response = await CreateService(db).ValidateAsync(
            new BatchWordRequest { Words = [CreateRow("hello")] },
            TestContext.Current.CancellationToken);

        response.IsValid.Should().BeTrue();
        (await db.Words.CountAsync(TestContext.Current.CancellationToken)).Should().Be(0);
        db.ChangeTracker.Entries<Word>().Should().BeEmpty();
    }

    [Fact]
    public void AddBusinessServicesShouldRegisterWordBatchService()
    {
        var services = new ServiceCollection();

        services.AddBusinessServices();

        services.Should().ContainSingle(descriptor =>
            descriptor.ServiceType == typeof(IWordBatchService) &&
            descriptor.ImplementationType == typeof(WordBatchService) &&
            descriptor.Lifetime == ServiceLifetime.Scoped);
    }

    private static WordBatchService CreateService(IApplicationDbContext db)
        => new(
            db,
            new CreateWordRequestValidator(),
            new PostgresDatabaseExceptionClassifier(),
            NullLogger<WordBatchService>.Instance);

    private static ApplicationDbContext CreateDbContext()
        => new(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options);

    private static Mock<IApplicationDbContext> WrapContext(ApplicationDbContext db)
    {
        var context = new Mock<IApplicationDbContext>();
        context.SetupGet(value => value.AudioResources).Returns(db.AudioResources);
        context.SetupGet(value => value.Words).Returns(db.Words);
        return context;
    }

    private static BatchWordRowRequest CreateRow(string headword)
        => new()
        {
            Headword = headword,
            Senses = [CreateSense(0)]
        };

    private static BatchWordSenseInput CreateSense(int sortOrder)
        => new()
        {
            PartOfSpeech = nameof(PartOfSpeech.Noun),
            Definition = "a greeting",
            SortOrder = sortOrder,
            Examples = []
        };

    private static BatchExampleSentenceInput CreateExample(int sortOrder)
        => new()
        {
            Sentence = "Hello there.",
            Translation = "你好。",
            SortOrder = sortOrder
        };

    private static AudioResource CreateAudio(
        string name,
        AudioResourceStatus status)
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
