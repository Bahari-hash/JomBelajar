using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;

namespace TinyLang.Services;

/// <summary>
/// 实现音频源绑定、编辑者管理状态机和登录用户播放授权规则。
/// </summary>
public sealed class AudioClipService : IAudioClipService
{
    private readonly IApplicationDbContext _db;
    private readonly IVideoDeliveryUrlService _deliveryUrlService;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 使用数据库、媒体交付、异常分类和时间源创建音频服务。
    /// </summary>
    public AudioClipService(
        IApplicationDbContext db,
        IVideoDeliveryUrlService deliveryUrlService,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider)
    {
        _db = db;
        _deliveryUrlService = deliveryUrlService;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<EditorAudioClipResponse> CreateAsync(
        Guid editorId,
        CreateAudioClipRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.MediaResources.SingleOrDefaultAsync(
            value => value.Id == request.SourceMediaResourceId,
            cancellationToken);
        if (source is null || source.UploaderId != editorId ||
            source.Module != ResourceModule.Audio)
        {
            throw NotFoundException.Create(ErrorCodes.AudioSourceInvalid);
        }
        if (source.Status != ResourceStatus.Active)
        {
            throw ConflictException.Create(ErrorCodes.AudioSourceNotActive);
        }
        if (await _db.AudioClips.AsNoTracking().AnyAsync(
            value => value.SourceMediaResourceId == source.Id,
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.AudioSourceAlreadyUsed);
        }
        EnsureKindIsValid(request.Kind);

        var audioClip = new AudioClip
        {
            OwnerId = editorId,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            LanguageTag = NormalizeLanguageTag(request.LanguageTag),
            Kind = request.Kind
        };
        audioClip.ProcessingJobs.Add(new AudioProcessingJob
        {
            AudioClip = audioClip,
            AudioClipId = audioClip.Id,
            OutputVersion = Guid.NewGuid()
        });
        _db.AudioClips.Add(audioClip);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_audio_clips_SourceMediaResourceId"))
        {
            throw ConflictException.Create(ErrorCodes.AudioSourceAlreadyUsed);
        }
        return await GetEditorByIdAsync(audioClip.Id, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<EditorAudioClipListItemResponse>> GetEditorListAsync(
        Guid editorId,
        EditorAudioClipListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AudioClips.AsNoTracking()
            .Where(value => value.OwnerId == editorId);
        if (request.ProcessingStatus is { } processingStatus)
        {
            query = query.Where(value => value.ProcessingStatus == processingStatus);
        }
        if (request.PublicationStatus is { } publicationStatus)
        {
            query = query.Where(value => value.PublicationStatus == publicationStatus);
        }
        if (request.Kind is { } kind)
        {
            query = query.Where(value => value.Kind == kind);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value => value.Title.ToUpper().Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new EditorAudioClipListItemResponse(
                value.Id,
                value.Title,
                value.LanguageTag,
                value.Kind,
                value.ProcessingStatus,
                value.PublicationStatus,
                value.DurationSeconds,
                value.LastFailureCode,
                value.UpdatedAt))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<EditorAudioClipResponse> GetEditorByIdAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var audioClip = await _db.AudioClips.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.Id == audioClipId && value.OwnerId == editorId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.AudioNotFound);
        return ToEditorResponse(audioClip);
    }

    /// <inheritdoc />
    public async Task<EditorAudioClipResponse> UpdateAsync(
        Guid audioClipId,
        Guid editorId,
        UpdateAudioClipRequest request,
        CancellationToken cancellationToken = default)
    {
        EnsureKindIsValid(request.Kind);
        var audioClip = await FindOwnedAudioClipAsync(
            audioClipId,
            editorId,
            cancellationToken);
        audioClip.Title = request.Title.Trim();
        audioClip.Description = NormalizeOptional(request.Description);
        audioClip.LanguageTag = NormalizeLanguageTag(request.LanguageTag);
        audioClip.Kind = request.Kind;
        audioClip.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithAudioConflictAsync(cancellationToken);
        return await GetEditorByIdAsync(audioClipId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorAudioClipResponse> PublishAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var audioClip = await FindOwnedAudioClipAsync(
            audioClipId,
            editorId,
            cancellationToken);
        if (audioClip.PublicationStatus == AudioPublicationStatus.Published)
        {
            return await GetEditorByIdAsync(audioClipId, editorId, cancellationToken);
        }
        if (audioClip.ProcessingStatus != AudioProcessingStatus.Ready)
        {
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        }
        audioClip.PublicationStatus = AudioPublicationStatus.Published;
        audioClip.PublishedAt = _timeProvider.GetUtcNow();
        audioClip.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithAudioConflictAsync(cancellationToken);
        return await GetEditorByIdAsync(audioClipId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorAudioClipResponse> UnpublishAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var audioClip = await FindOwnedAudioClipAsync(
            audioClipId,
            editorId,
            cancellationToken);
        if (audioClip.PublicationStatus == AudioPublicationStatus.Unpublished)
        {
            return await GetEditorByIdAsync(audioClipId, editorId, cancellationToken);
        }
        if (audioClip.PublicationStatus != AudioPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        }
        audioClip.PublicationStatus = AudioPublicationStatus.Unpublished;
        audioClip.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithAudioConflictAsync(cancellationToken);
        return await GetEditorByIdAsync(audioClipId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorAudioClipResponse> RetryAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var audioClip = await FindOwnedAudioClipAsync(
            audioClipId,
            editorId,
            cancellationToken);
        if (audioClip.ProcessingStatus != AudioProcessingStatus.Failed)
        {
            throw ConflictException.Create(ErrorCodes.AudioRetryConflict);
        }
        audioClip.ProcessingStatus = AudioProcessingStatus.Queued;
        audioClip.LastFailureCode = null;
        audioClip.ConcurrencyStamp = Guid.NewGuid();
        _db.AudioProcessingJobs.Add(new AudioProcessingJob
        {
            AudioClipId = audioClip.Id,
            AudioClip = audioClip,
            OutputVersion = Guid.NewGuid()
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_audio_processing_jobs_AudioClipId"))
        {
            throw ConflictException.Create(ErrorCodes.AudioRetryConflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.Create(ErrorCodes.AudioRetryConflict);
        }
        return await GetEditorByIdAsync(audioClipId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AudioPlaybackResponse> GetPlaybackAsync(
        Guid audioClipId,
        CancellationToken cancellationToken = default)
    {
        var audioClip = await _db.AudioClips.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == audioClipId &&
                value.ProcessingStatus == AudioProcessingStatus.Ready &&
                value.PublicationStatus == AudioPublicationStatus.Published,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.AudioNotFound);
        if (audioClip.DurationSeconds is not { } durationSeconds ||
            audioClip.CurrentOutputVersion is not { } outputVersion ||
            audioClip.OutputObjectName is null)
        {
            throw NotFoundException.Create(ErrorCodes.AudioNotFound);
        }
        var outputPrefix = GetOutputPrefix(
            audioClip.Id,
            outputVersion);
        var expectedObjectName = $"{outputPrefix}audio.mp3";
        if (!string.Equals(
            audioClip.OutputObjectName,
            expectedObjectName,
            StringComparison.Ordinal))
        {
            throw NotFoundException.Create(ErrorCodes.AudioNotFound);
        }
        var delivery = _deliveryUrlService.CreateUrl(expectedObjectName, outputPrefix);
        return new AudioPlaybackResponse(
            delivery.Url,
            delivery.ExpiresAt,
            durationSeconds,
            audioClip.LanguageTag,
            audioClip.Kind);
    }

    /// <summary>
    /// 查找当前编辑者拥有的可变音频，不泄漏其他 owner 的实体存在性。
    /// </summary>
    private async Task<AudioClip> FindOwnedAudioClipAsync(
        Guid audioClipId,
        Guid editorId,
        CancellationToken cancellationToken)
        => await _db.AudioClips.SingleOrDefaultAsync(
            value => value.Id == audioClipId && value.OwnerId == editorId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.AudioNotFound);

    /// <summary>
    /// 保存音频并发变更并映射为稳定状态冲突。
    /// </summary>
    private async Task SaveWithAudioConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        }
    }

    /// <summary>
    /// 将音频实体映射为不包含内部对象路径的管理响应。
    /// </summary>
    private static EditorAudioClipResponse ToEditorResponse(AudioClip audioClip)
        => new(
            audioClip.Id,
            audioClip.SourceMediaResourceId,
            audioClip.Title,
            audioClip.Description,
            audioClip.LanguageTag,
            audioClip.Kind,
            audioClip.ProcessingStatus,
            audioClip.PublicationStatus,
            audioClip.DurationSeconds,
            audioClip.SampleRate,
            audioClip.Channels,
            audioClip.ContainerFormat,
            audioClip.SourceCodec,
            audioClip.LastFailureCode,
            audioClip.PublishedAt,
            audioClip.CreatedAt,
            audioClip.UpdatedAt);

    /// <summary>
    /// 创建具有确定总页数的通用分页响应。
    /// </summary>
    private static PagedResponse<T> CreatePage<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(
            items,
            page,
            pageSize,
            totalCount,
            (int)Math.Ceiling((double)totalCount / pageSize));

    /// <summary>
    /// 规范化可选展示文本并将空白值转换为 null。
    /// </summary>
    private static string? NormalizeOptional(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>
    /// 以小写形式持久化已由 validator 验证的语言标签。
    /// </summary>
    private static string NormalizeLanguageTag(string value)
        => value.Trim().ToLowerInvariant();

    /// <summary>
    /// 在业务边界防御无效音频用途枚举。
    /// </summary>
    private static void EnsureKindIsValid(AudioClipKind kind)
    {
        if (!Enum.IsDefined(kind))
        {
            throw new RequestValidationException(ErrorCodes.AudioKindInvalid);
        }
    }

    /// <summary>
    /// 构建只包含服务端标识的不可变音频输出前缀。
    /// </summary>
    private static string GetOutputPrefix(Guid audioClipId, Guid outputVersion)
        => $"audios/{audioClipId:N}/outputs/{outputVersion:N}/";
}
