using System.IO;
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
    private const int MaximumAudioNameLength = 255;
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
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var media = await _mediaResourceService.CreatePendingResourceAndPresignAsync(
            adminId,
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            ResourceModule.Audio,
            cancellationToken);
        var response = await CreateAudioResourceAsync(
            adminId,
            request.OriginalName,
            media.ResourceId,
            media.PresignedUrl,
            null,
            null,
            null,
            null,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return response;
    }

    public async Task<AudioUploadInitializationResponse> InitializeMultipartUploadAsync(
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        var multipart = await _mediaResourceService.CreateMultipartUploadAsync(
            adminId,
            request.OriginalName,
            request.Extension,
            request.Size,
            request.ContentType,
            ResourceModule.Audio,
            cancellationToken);
        try
        {
            var response = await CreateAudioResourceAsync(
                adminId,
                request.OriginalName,
                multipart.ResourceId,
                null,
                multipart.SessionId,
                checked((int)multipart.PartSize),
                multipart.PartCount,
                multipart.ExpiresAt,
                cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return response;
        }
        catch
        {
            await TryAbortMultipartAsync(
                multipart.SessionId,
                adminId,
                cancellationToken);
            throw;
        }
    }

    public async Task ConfirmUploadAsync(
        Guid audioResourceId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireAudioResourceLockAsync(audioResourceId, cancellationToken);
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        if (resource.Status is AudioResourceStatus.Queued or
            AudioResourceStatus.Processing or
            AudioResourceStatus.Ready)
        {
            await transaction.CommitAsync(cancellationToken);
            return;
        }
        if (resource.Status != AudioResourceStatus.Uploading)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        try
        {
            await _mediaResourceService.ConfirmAsync(
                resource.SourceMediaResourceId,
                adminId,
                cancellationToken);
        }
        catch (ConflictException exception) when (
            exception.ErrorCode is ErrorCodes.MediaResourceUploadIncomplete or
                ErrorCodes.MediaResourceFinalizing)
        {
            throw ConflictException.Create(ErrorCodes.AudioUploadIncomplete);
        }
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
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<MultipartPartPresignResult>> PresignMultipartPartsAsync(
        Guid sessionId,
        Guid adminId,
        IReadOnlyCollection<int> partNumbers,
        CancellationToken cancellationToken = default)
    {
        await EnsureAudioMultipartSessionAsync(sessionId, adminId, cancellationToken);
        return await _mediaResourceService.PresignMultipartPartsAsync(
            sessionId, adminId, partNumbers, cancellationToken);
    }

    public async Task<MultipartUploadStatusResult> GetMultipartUploadAsync(
        Guid sessionId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        await EnsureAudioMultipartSessionAsync(sessionId, adminId, cancellationToken);
        return await _mediaResourceService.GetMultipartUploadAsync(
            sessionId, adminId, cancellationToken);
    }

    public async Task<MultipartUploadStatusResult> CompleteMultipartUploadAsync(
        Guid sessionId,
        Guid adminId,
        IReadOnlyCollection<ObjectStorageUploadedPart> parts,
        CancellationToken cancellationToken = default)
    {
        await EnsureAudioMultipartSessionAsync(sessionId, adminId, cancellationToken);
        return await _mediaResourceService.CompleteMultipartUploadAsync(
            sessionId, adminId, parts, cancellationToken);
    }

    public async Task AbortMultipartUploadAsync(
        Guid sessionId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        var mediaResourceId = await EnsureAudioMultipartSessionAsync(
            sessionId,
            adminId,
            cancellationToken);
        var audioResourceId = await _db.AudioResources.AsNoTracking()
            .Where(value => value.SourceMediaResourceId == mediaResourceId)
            .Select(value => value.Id)
            .SingleAsync(cancellationToken);
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireAudioResourceLockAsync(audioResourceId, cancellationToken);
        var audioResource = await FindAudioResourceAsync(
            audioResourceId,
            cancellationToken);
        await _mediaResourceService.AbortMultipartUploadAsync(
            sessionId, adminId, cancellationToken);
        if (audioResource.SourceMediaResourceId == mediaResourceId &&
            audioResource.Status == AudioResourceStatus.Uploading)
        {
            audioResource.Status = AudioResourceStatus.Failed;
            audioResource.LastFailureCode = ErrorCodes.AudioUploadIncomplete.ToString();
            audioResource.ConcurrencyStamp = Guid.NewGuid();
            await SaveAsync(cancellationToken);
        }
        await transaction.CommitAsync(cancellationToken);
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

    public async Task<AudioUploadInitializationResponse> RetryUploadAsync(
        Guid audioResourceId,
        Guid adminId,
        InitializeAudioUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireAudioResourceLockAsync(audioResourceId, cancellationToken);
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        if (resource.Status != AudioResourceStatus.Failed)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        var previousSource = await _db.MediaResources.SingleAsync(
            value => value.Id == resource.SourceMediaResourceId,
            cancellationToken);
        var media = request.Size > 0
            ? await _mediaResourceService.CreatePendingResourceAndPresignAsync(
                adminId, request.OriginalName, request.Extension, request.Size,
                request.ContentType, ResourceModule.Audio, cancellationToken)
            : throw new RequestValidationException(ErrorCodes.AudioUploadIncomplete);
        var source = await _db.MediaResources.SingleAsync(
            value => value.Id == media.ResourceId, cancellationToken);
        previousSource.Status = ResourceStatus.Expired;
        previousSource.Url = null;
        previousSource.StagingObjectName ??= previousSource.ObjectName;
        previousSource.UploadExpiresAt = null;
        previousSource.ConcurrencyStamp = Guid.NewGuid();
        resource.ReplaceSource(adminId, source);
        await SaveAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new AudioUploadInitializationResponse(
            resource.Id,
            media.ResourceId,
            media.PresignedUrl,
            null,
            null,
            null,
            null);
    }

    public async Task<AdminAudioResourceResponse> ReprocessAsync(
        Guid audioResourceId,
        Guid adminId,
        CancellationToken cancellationToken = default)
    {
        await using var transaction = await _db.BeginTransactionAsync(cancellationToken);
        await _db.AcquireAudioResourceLockAsync(audioResourceId, cancellationToken);
        var resource = await FindAudioResourceAsync(audioResourceId, cancellationToken);
        if (resource.Status != AudioResourceStatus.Failed)
            throw ConflictException.Create(ErrorCodes.AudioStatusConflict);
        var sourceIsActiveAudio = await _db.MediaResources.AsNoTracking()
            .AnyAsync(value => value.Id == resource.SourceMediaResourceId &&
                value.Module == ResourceModule.Audio &&
                value.Status == ResourceStatus.Active,
                cancellationToken);
        if (!sourceIsActiveAudio)
            throw ConflictException.Create(ErrorCodes.AudioUploadIncomplete);
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
        await transaction.CommitAsync(cancellationToken);
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
        Guid? sessionId,
        int? partSize,
        int? partCount,
        DateTimeOffset? expiresAt,
        CancellationToken cancellationToken)
    {
        for (var sequence = 1; sequence < int.MaxValue; sequence++)
        {
            var candidateName = CreateNameCandidate(originalName, sequence);
            var normalizedName = AudioResource.NormalizeName(candidateName);
            if (await _db.AudioResources.AsNoTracking()
                    .AnyAsync(value => value.NormalizedName == normalizedName, cancellationToken))
            {
                continue;
            }

            var resource = AudioResource.Create(adminId, candidateName, mediaResourceId);
            _db.AudioResources.Add(resource);
            try
            {
                await SaveAsync(cancellationToken);
                return new AudioUploadInitializationResponse(
                    resource.Id,
                    mediaResourceId,
                    presignedUrl,
                    sessionId,
                    partSize,
                    partCount,
                    expiresAt);
            }
            catch (DbUpdateException exception) when (
                _databaseExceptionClassifier.IsUniqueConstraintViolation(
                    exception,
                    "IX_audio_resources_NormalizedName"))
            {
                _db.AudioResources.Remove(resource);
            }
        }

        throw ConflictException.Create(ErrorCodes.AudioNameConflict);
    }

    private static string CreateNameCandidate(string originalName, int sequence)
    {
        var displayName = originalName.Trim();
        if (sequence == 1)
            return displayName;

        var extension = Path.GetExtension(displayName);
        var stem = Path.GetFileNameWithoutExtension(displayName);
        var suffix = $" ({sequence})";
        var maximumStemLength = MaximumAudioNameLength - suffix.Length - extension.Length;
        if (stem.Length > maximumStemLength)
            stem = stem[..maximumStemLength];
        return $"{stem}{suffix}{extension}";
    }

    private async Task TryAbortMultipartAsync(
        Guid sessionId,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        try
        {
            await _mediaResourceService.AbortMultipartUploadAsync(
                sessionId,
                adminId,
                cancellationToken);
        }
        catch (Exception exception) when (
            exception is not OperationCanceledException)
        {
            // The initialization failure remains authoritative; maintenance can clean an orphaned provider session.
        }
    }

    private async Task<Guid> EnsureAudioMultipartSessionAsync(
        Guid sessionId,
        Guid adminId,
        CancellationToken cancellationToken)
    {
        var session = await _db.MultipartUploadSessions.AsNoTracking()
            .Where(value => value.Id == sessionId)
            .Select(value => new
            {
                value.UploaderId,
                value.MediaResourceId,
                value.MediaResource.Module
            })
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.MultipartUploadNotFound);
        if (session.UploaderId != adminId)
            throw ForbiddenException.Create(ErrorCodes.MultipartUploadOwnershipMismatch);
        if (session.Module != ResourceModule.Audio ||
            !await _db.AudioResources.AsNoTracking().AnyAsync(
                value => value.SourceMediaResourceId == session.MediaResourceId,
                cancellationToken))
        {
            throw NotFoundException.Create(ErrorCodes.MultipartUploadNotFound);
        }
        return session.MediaResourceId;
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
