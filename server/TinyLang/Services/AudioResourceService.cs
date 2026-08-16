using Microsoft.EntityFrameworkCore;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Models;

namespace TinyLang.Services;

/// <summary>
/// 管理独立音频资源的上传生命周期、状态查询和播放授权。
/// </summary>
public sealed class AudioResourceService : IAudioResourceService
{
    private readonly IApplicationDbContext _db;
    private readonly IMediaResourceService _mediaResourceService;
    private readonly IVideoDeliveryUrlService _deliveryUrlService;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly TimeProvider _timeProvider;

    public AudioResourceService(
        IApplicationDbContext db,
        IMediaResourceService mediaResourceService,
        IVideoDeliveryUrlService deliveryUrlService,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        TimeProvider timeProvider)
    {
        _db = db;
        _mediaResourceService = mediaResourceService;
        _deliveryUrlService = deliveryUrlService;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _timeProvider = timeProvider;
    }

    public async Task<AudioUploadInitializationResponse> InitializeSimpleUploadAsync(
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var media = await _mediaResourceService.CreatePendingResourceAndPresignAsync(
            adminId,
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            ResourceModule.Audio,
            cancellationToken);
        return await CreateAudioResourceAsync(
            adminId,
            request.OriginalName,
            media.ResourceId,
            media.PresignedUrl,
            media.ObjectName,
            null,
            null,
            null,
            null,
            cancellationToken);
    }

    public async Task<AudioUploadInitializationResponse> InitializeMultipartUploadAsync(
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var multipart = await _mediaResourceService.CreateMultipartUploadAsync(
            adminId,
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            ResourceModule.Audio,
            cancellationToken);
        var media = await _db.MediaResources.AsNoTracking()
            .SingleAsync(value => value.Id == multipart.ResourceId, cancellationToken);
        return await CreateAudioResourceAsync(
            adminId,
            request.OriginalName,
            multipart.ResourceId,
            null,
            media.StagingObjectName ?? media.ObjectName,
            multipart.SessionId,
            checked((int)multipart.PartSize),
            multipart.PartCount,
            multipart.ExpiresAt,
            cancellationToken);
    }

    public async Task ConfirmUploadAsync(
        Guid audioResourceId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        if (resource.Status is AudioResourceStatus.Queued or
            AudioResourceStatus.Processing or
            AudioResourceStatus.Ready)
            return;
        if (resource.Status != AudioResourceStatus.Uploading)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        await _mediaResourceService.ConfirmAsync(
            resource.SourceMediaResourceId,
            adminId,
            cancellationToken);
        resource.Queue(adminId);
        var activeJob = await _db.AudioProcessingJobs.AnyAsync(
            value => value.AudioResourceId == resource.Id &&
                (value.Status == AudioProcessingJobStatus.Queued ||
                 value.Status == AudioProcessingJobStatus.Processing),
            cancellationToken);
        if (!activeJob)
            _db.AudioProcessingJobs.Add(new AudioProcessingJob
            {
                AudioResourceId = resource.Id,
                AudioResource = resource,
                OutputVersion = Guid.NewGuid()
            });
        await SaveAsync(cancellationToken);
    }

    public async Task<PagedResponse<AdminAudioResourceListItemResponse>> GetAdminListAsync(
        AdminAudioResourceListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AudioResources.AsNoTracking();
        if (request.Status is { } status)
            query = query.Where(value => value.Status == status);
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = AudioResource.NormalizeName(request.Keyword);
            query = query.Where(value => value.NormalizedName.Contains(keyword));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new AdminAudioResourceListItemResponse(
                value.Id,
                value.Name,
                value.Status,
                value.DurationSeconds,
                value.LastFailureCode,
                value.UpdatedAt))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    public async Task<AdminAudioResourceResponse> GetAdminByIdAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default)
        => ToResponse(await FindAudioResourceAsync(audioResourceId, cancellationToken));

    public async Task<AdminAudioResourceResponse> RenameAsync(
        Guid audioResourceId,
        Guid adminId,
        RenameAudioResourceRequest request,
        CancellationToken cancellationToken = default)
    {
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        var normalizedName = AudioResource.NormalizeName(request.Name);
        var duplicate = await _db.AudioResources.AsNoTracking()
            .AnyAsync(value => value.Id != audioResourceId &&
                value.NormalizedName == normalizedName, cancellationToken);
        if (duplicate)
            throw ConflictException.Create(ErrorCodes.AudioNameConflict);
        resource.Rename(adminId, request.Name);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_audio_resources_NormalizedName"))
        {
            throw ConflictException.Create(ErrorCodes.AudioNameConflict);
        }
        return ToResponse(resource);
    }

    public async Task<AdminAudioResourceResponse> RetryUploadAsync(
        Guid audioResourceId,
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        if (resource.Status != AudioResourceStatus.Failed)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        var media = request.Size > 0
            ? await _mediaResourceService.CreatePendingResourceAndPresignAsync(
                adminId, request.OriginalName, request.Extension, request.Size,
                request.ContentType, ResourceModule.Audio, cancellationToken)
            : throw new RequestValidationException(ErrorCodes.AudioUploadIncomplete);
        var source = await _db.MediaResources.SingleAsync(
            value => value.Id == media.ResourceId, cancellationToken);
        resource.ReplaceSource(adminId, source);
        await SaveAsync(cancellationToken);
        return ToResponse(resource);
    }

    public async Task<AdminAudioResourceResponse> ReprocessAsync(
        Guid audioResourceId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        if (resource.Status != AudioResourceStatus.Failed)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        var activeJob = await _db.AudioProcessingJobs.AnyAsync(
            value => value.AudioResourceId == resource.Id &&
                (value.Status == AudioProcessingJobStatus.Queued ||
                 value.Status == AudioProcessingJobStatus.Processing),
            cancellationToken);
        if (activeJob)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        resource.Queue(adminId);
        _db.AudioProcessingJobs.Add(new AudioProcessingJob
        {
            AudioResourceId = resource.Id,
            AudioResource = resource,
            OutputVersion = Guid.NewGuid()
        });
        await SaveAsync(cancellationToken);
        return ToResponse(resource);
    }

    public async Task DeleteAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default)
    {
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        _db.AudioResources.Remove(resource);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            throw ConflictException.Create(ErrorCodes.AudioInUse);
        }
    }

    public async Task<AudioResourcePlaybackResponse> GetPlaybackAsync(
        Guid audioResourceId,
        CancellationToken cancellationToken = default)
    {
        var resource = await _db.AudioResources.AsNoTracking()
            .SingleOrDefaultAsync(value => value.Id == audioResourceId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.AudioNotFound);
        if (resource.Status != AudioResourceStatus.Ready)
            throw ConflictException.Create(ErrorCodes.AudioNotReady);
        if (resource.DurationSeconds is not { } duration ||
            resource.CurrentOutputVersion is not { } outputVersion ||
            resource.OutputObjectName is null)
            throw ConflictException.Create(ErrorCodes.AudioNotReady);
        var prefix = GetOutputPrefix(resource.Id, outputVersion);
        var expectedObjectName = $"{prefix}audio.mp3";
        if (!string.Equals(resource.OutputObjectName, expectedObjectName, StringComparison.Ordinal))
            throw ConflictException.Create(ErrorCodes.AudioNotReady);
        var delivery = _deliveryUrlService.CreateUrl(expectedObjectName, prefix);
        return new AudioResourcePlaybackResponse(delivery.Url, delivery.ExpiresAt, duration);
    }

    private async Task<AudioUploadInitializationResponse> CreateAudioResourceAsync(
        Guid adminId,
        string originalName,
        Guid mediaResourceId,
        string? presignedUrl,
        string objectName,
        Guid? sessionId,
        int? partSize,
        int? partCount,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        var resource = AudioResource.Create(adminId, originalName, mediaResourceId);
        _db.AudioResources.Add(resource);
        try
        {
            await SaveAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_audio_resources_NormalizedName"))
        {
            throw ConflictException.Create(ErrorCodes.AudioNameConflict);
        }
        return new AudioUploadInitializationResponse(
            resource.Id,
            mediaResourceId,
            presignedUrl,
            objectName,
            sessionId,
            partSize,
            partCount,
            expiresAt);
    }

    private async Task<AudioResource> FindAudioResourceAsync(
        Guid id,
        CancellationToken cancellationToken)
        => await _db.AudioResources.SingleOrDefaultAsync(
            value => value.Id == id, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.AudioNotFound);

    private async Task SaveAsync(CancellationToken cancellationToken)
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

    private static AdminAudioResourceResponse ToResponse(AudioResource value)
        => new(
            value.Id,
            value.Name,
            value.Status,
            value.DurationSeconds,
            value.SampleRate,
            value.Channels,
            value.ContainerFormat,
            value.SourceCodec,
            value.LastFailureCode,
            value.CurrentOutputVersion,
            value.ConcurrencyStamp,
            value.CreatedAt,
            value.UpdatedAt);

    private static PagedResponse<T> CreatePage<T>(
        IReadOnlyList<T> items,
        int page,
        int pageSize,
        int totalCount)
        => new(items, page, pageSize, totalCount,
            (int)Math.Ceiling((double)totalCount / pageSize));

    private static string GetOutputPrefix(Guid resourceId, Guid outputVersion)
        => $"audios/{resourceId:N}/outputs/{outputVersion:N}/";
}
