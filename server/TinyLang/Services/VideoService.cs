using System.IO;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 实现视频源绑定、状态机、字幕、登录播放授权和播放进度业务规则。
/// </summary>
public sealed class VideoService : IVideoService
{
    private readonly IApplicationDbContext _db;
    private readonly IObjectStorageService _objectStorage;
    private readonly IVideoDeliveryUrlService _deliveryUrlService;
    private readonly IUserVideoProgressStore _progressStore;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly VideoProgressSettings _progressSettings;
    private readonly UploadSettings _uploadSettings;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<VideoService> _logger;

    /// <summary>
    /// 使用数据库、存储、delivery、进度持久化和业务配置创建视频服务。
    /// </summary>
    public VideoService(
        IApplicationDbContext db,
        IObjectStorageService objectStorage,
        IVideoDeliveryUrlService deliveryUrlService,
        IUserVideoProgressStore progressStore,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        IOptions<VideoProgressSettings> progressOptions,
        IOptions<UploadSettings> uploadOptions,
        TimeProvider timeProvider,
        ILogger<VideoService> logger)
    {
        _db = db;
        _objectStorage = objectStorage;
        _deliveryUrlService = deliveryUrlService;
        _progressStore = progressStore;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _progressSettings = progressOptions.Value;
        _uploadSettings = uploadOptions.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<EditorVideoResponse> CreateAsync(
        Guid editorId,
        CreateVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.MediaResources.SingleOrDefaultAsync(
            value => value.Id == request.SourceMediaResourceId,
            cancellationToken);
        if (source is null || source.UploaderId != editorId ||
            source.Module != ResourceModule.CourseVideo)
        {
            throw NotFoundException.Create(ErrorCodes.VideoSourceInvalid);
        }
        if (source.Status != ResourceStatus.Active)
        {
            throw ConflictException.Create(ErrorCodes.VideoSourceNotActive);
        }
        if (await _db.Videos.AsNoTracking().AnyAsync(
            value => value.SourceMediaResourceId == source.Id,
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.VideoSourceAlreadyUsed);
        }
        var categories = await LoadUsableCategoriesAsync(
            request.CategoryIds,
            cancellationToken);

        var video = new Video
        {
            OwnerId = editorId,
            SourceMediaResourceId = source.Id,
            SourceMediaResource = source,
            Title = request.Title.Trim(),
            Description = NormalizeOptional(request.Description),
            OriginalLanguage = NormalizeLanguageTag(request.OriginalLanguage)
        };
        video.ProcessingJobs.Add(new VideoProcessingJob
        {
            Video = video,
            VideoId = video.Id,
            OutputVersion = Guid.NewGuid()
        });
        foreach (var category in categories)
        {
            video.CategoryAssignments.Add(new VideoCategoryAssignment
            {
                Video = video,
                VideoId = video.Id,
                VideoCategory = category,
                VideoCategoryId = category.Id
            });
        }
        _db.Videos.Add(video);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_videos_SourceMediaResourceId"))
        {
            throw ConflictException.Create(ErrorCodes.VideoSourceAlreadyUsed);
        }
        return await GetEditorByIdAsync(video.Id, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<EditorVideoListItemResponse>> GetEditorListAsync(
        Guid editorId,
        EditorVideoListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Videos.AsNoTracking().Where(value => value.OwnerId == editorId);
        if (request.ProcessingStatus is { } processingStatus)
        {
            query = query.Where(value => value.ProcessingStatus == processingStatus);
        }
        if (request.PublicationStatus is { } publicationStatus)
        {
            query = query.Where(value => value.PublicationStatus == publicationStatus);
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value => value.Title.ToUpper().Contains(keyword));
        }
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(value => value.CategoryAssignments
                .Any(assignment => assignment.VideoCategoryId == categoryId));
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new EditorVideoListItemResponse(
                value.Id,
                value.Title,
                value.OriginalLanguage,
                value.ProcessingStatus,
                value.PublicationStatus,
                value.DurationSeconds,
                value.LastFailureCode,
                value.UpdatedAt,
                value.CategoryAssignments
                    .OrderBy(assignment => assignment.VideoCategory.Name)
                    .ThenBy(assignment => assignment.VideoCategoryId)
                    .Select(assignment => new EditorVideoCategorySummaryResponse(
                        assignment.VideoCategory.Id,
                        assignment.VideoCategory.Name,
                        assignment.VideoCategory.Slug,
                        assignment.VideoCategory.IsActive))
                    .ToList()))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<EditorVideoResponse> GetEditorByIdAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var video = await EditorDetailsQuery()
            .SingleOrDefaultAsync(
                value => value.Id == videoId && value.OwnerId == editorId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        return ToEditorResponse(video);
    }

    /// <inheritdoc />
    public async Task<EditorVideoResponse> UpdateAsync(
        Guid videoId,
        Guid editorId,
        UpdateVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindOwnedVideoAsync(videoId, editorId, cancellationToken);
        var categories = await LoadUsableCategoriesAsync(
            request.CategoryIds,
            cancellationToken);
        var existingAssignments = await _db.VideoCategoryAssignments
            .Where(value => value.VideoId == videoId)
            .ToListAsync(cancellationToken);
        SynchronizeCategories(videoId, existingAssignments, categories);
        video.Title = request.Title.Trim();
        video.Description = NormalizeOptional(request.Description);
        video.OriginalLanguage = NormalizeLanguageTag(request.OriginalLanguage);
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetEditorByIdAsync(videoId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorVideoResponse> PublishAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var video = await FindOwnedVideoAsync(videoId, editorId, cancellationToken);
        if (video.PublicationStatus == VideoPublicationStatus.Published)
        {
            return await GetEditorByIdAsync(videoId, editorId, cancellationToken);
        }
        if (video.ProcessingStatus != VideoProcessingStatus.Ready)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
        video.PublicationStatus = VideoPublicationStatus.Published;
        video.PublishedAt = _timeProvider.GetUtcNow();
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetEditorByIdAsync(videoId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorVideoResponse> UnpublishAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var video = await FindOwnedVideoAsync(videoId, editorId, cancellationToken);
        if (video.PublicationStatus == VideoPublicationStatus.Unpublished)
        {
            return await GetEditorByIdAsync(videoId, editorId, cancellationToken);
        }
        if (video.PublicationStatus != VideoPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
        video.PublicationStatus = VideoPublicationStatus.Unpublished;
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetEditorByIdAsync(videoId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorVideoResponse> RetryAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        var video = await FindOwnedVideoAsync(videoId, editorId, cancellationToken);
        if (video.ProcessingStatus != VideoProcessingStatus.Failed)
        {
            throw ConflictException.Create(ErrorCodes.VideoRetryConflict);
        }
        video.ProcessingStatus = VideoProcessingStatus.Queued;
        video.LastFailureCode = null;
        video.ConcurrencyStamp = Guid.NewGuid();
        _db.VideoProcessingJobs.Add(new VideoProcessingJob
        {
            VideoId = video.Id,
            OutputVersion = Guid.NewGuid()
        });
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_video_processing_jobs_VideoId"))
        {
            throw ConflictException.Create(ErrorCodes.VideoRetryConflict);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.Create(ErrorCodes.VideoRetryConflict);
        }
        return await GetEditorByIdAsync(videoId, editorId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<EditorVideoSubtitleResponse> AddSubtitleAsync(
        Guid videoId,
        Guid editorId,
        AddVideoSubtitleRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindOwnedVideoAsync(videoId, editorId, cancellationToken);
        var resource = await _db.MediaResources.SingleOrDefaultAsync(
            value => value.Id == request.MediaResourceId,
            cancellationToken);
        if (resource is null || resource.UploaderId != editorId ||
            resource.Module != ResourceModule.VideoSubtitle ||
            resource.Status != ResourceStatus.Active)
        {
            throw NotFoundException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
        if (await _db.VideoSubtitles.AsNoTracking().AnyAsync(
            value => value.MediaResourceId == resource.Id,
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
        var languageTag = NormalizeLanguageTag(request.LanguageTag);
        if (await _db.VideoSubtitles.AsNoTracking().AnyAsync(
            value => value.VideoId == videoId && value.LanguageTag == languageTag,
            cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleLanguageConflict);
        }
        await ValidateWebVttAsync(resource, cancellationToken);

        if (request.IsDefault)
        {
            var currentDefaults = await _db.VideoSubtitles
                .Where(value => value.VideoId == videoId && value.IsDefault)
                .ToListAsync(cancellationToken);
            foreach (var currentDefault in currentDefaults)
            {
                currentDefault.IsDefault = false;
            }
        }
        var subtitle = new VideoSubtitle
        {
            VideoId = video.Id,
            MediaResourceId = resource.Id,
            LanguageTag = languageTag,
            DisplayName = request.DisplayName.Trim(),
            IsDefault = request.IsDefault,
            SortOrder = request.SortOrder
        };
        _db.VideoSubtitles.Add(subtitle);
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception) when (
            _databaseExceptionClassifier.IsUniqueConstraintViolation(
                exception,
                "IX_video_subtitles_MediaResourceId",
                "IX_video_subtitles_VideoId_LanguageTag",
                "IX_video_subtitles_VideoId"))
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleLanguageConflict);
        }
        return ToSubtitleResponse(subtitle);
    }

    /// <inheritdoc />
    public async Task RemoveSubtitleAsync(
        Guid videoId,
        Guid subtitleId,
        Guid editorId,
        CancellationToken cancellationToken = default)
    {
        _ = await FindOwnedVideoAsync(videoId, editorId, cancellationToken);
        var subtitle = await _db.VideoSubtitles
            .Include(value => value.MediaResource)
            .SingleOrDefaultAsync(
                value => value.Id == subtitleId && value.VideoId == videoId,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoSubtitleInvalid);
        _db.VideoSubtitles.Remove(subtitle);
        subtitle.MediaResource.Status = ResourceStatus.Aborted;
        subtitle.MediaResource.ConcurrencyStamp = Guid.NewGuid();
        await _db.SaveChangesAsync(cancellationToken);
        try
        {
            await _objectStorage.DeleteObjectAsync(
                subtitle.MediaResource.ObjectName,
                cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            _logger.LogWarning(
                "Video subtitle object cleanup failed for subtitle {SubtitleId} with {FailureType}",
                subtitleId,
                exception.GetType().Name);
        }
    }

    /// <inheritdoc />
    public async Task<PagedResponse<VideoCatalogItemResponse>> GetCatalogAsync(
        VideoCatalogRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = PublishedVideosQuery();
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var keyword = request.Keyword.Trim().ToUpperInvariant();
            query = query.Where(value => value.Title.ToUpper().Contains(keyword));
        }
        if (request.CategoryId is { } categoryId)
        {
            query = query.Where(value => value.CategoryAssignments.Any(assignment =>
                assignment.VideoCategoryId == categoryId &&
                assignment.VideoCategory.IsActive));
        }
        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.PublishedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new VideoCatalogItemResponse(
                value.Id,
                value.Title,
                value.Description,
                value.OriginalLanguage,
                value.DurationSeconds!.Value,
                value.PublishedAt!.Value,
                value.CategoryAssignments
                    .Where(assignment => assignment.VideoCategory.IsActive)
                    .OrderBy(assignment => assignment.VideoCategory.Name)
                    .ThenBy(assignment => assignment.VideoCategoryId)
                    .Select(assignment => new VideoCategorySummaryResponse(
                        assignment.VideoCategory.Id,
                        assignment.VideoCategory.Name,
                        assignment.VideoCategory.Slug))
                    .ToList()))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<VideoDetailsResponse> GetDetailsAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        return await PublishedVideosQuery()
            .Where(value => value.Id == videoId)
            .Select(value => new VideoDetailsResponse(
                value.Id,
                value.Title,
                value.Description,
                value.OriginalLanguage,
                value.DurationSeconds!.Value,
                value.DisplayWidth!.Value,
                value.DisplayHeight!.Value,
                value.PublishedAt!.Value,
                value.CategoryAssignments
                    .Where(assignment => assignment.VideoCategory.IsActive)
                    .OrderBy(assignment => assignment.VideoCategory.Name)
                    .ThenBy(assignment => assignment.VideoCategoryId)
                    .Select(assignment => new VideoCategorySummaryResponse(
                        assignment.VideoCategory.Id,
                        assignment.VideoCategory.Name,
                        assignment.VideoCategory.Slug))
                    .ToList()))
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);
    }

    /// <inheritdoc />
    public async Task<VideoPlaybackResponse> GetPlaybackAsync(
        Guid videoId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var video = await PublishedVideosQuery()
            .Include(value => value.Subtitles.OrderBy(subtitle => subtitle.SortOrder))
                .ThenInclude(value => value.MediaResource)
            .SingleOrDefaultAsync(value => value.Id == videoId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        if (video.MasterPlaylistObjectName is null ||
            video.CurrentOutputVersion is null ||
            video.DurationSeconds is null)
        {
            throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        }

        var outputPrefix = GetObjectDirectory(video.MasterPlaylistObjectName);
        var masterUrl = _deliveryUrlService.CreateUrl(
            video.MasterPlaylistObjectName,
            outputPrefix);
        var posterUrl = video.PosterObjectName is null
            ? null
            : _deliveryUrlService.CreateUrl(video.PosterObjectName, outputPrefix).Url;
        var subtitles = video.Subtitles
            .Select(subtitle => new VideoPlaybackSubtitleResponse(
                subtitle.LanguageTag,
                subtitle.DisplayName,
                subtitle.IsDefault,
                _deliveryUrlService.CreateUrl(
                    subtitle.MediaResource.ObjectName,
                    GetObjectDirectory(subtitle.MediaResource.ObjectName)).Url))
            .ToArray();
        var progress = await _db.UserVideoProgress.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UserId == userId && value.VideoId == videoId,
                cancellationToken);
        return new VideoPlaybackResponse(
            masterUrl.Url,
            posterUrl,
            masterUrl.ExpiresAt,
            video.DurationSeconds.Value,
            progress?.PositionSeconds ?? 0,
            progress?.IsCompleted ?? false,
            subtitles);
    }

    /// <inheritdoc />
    public async Task UpdateProgressAsync(
        Guid videoId,
        Guid userId,
        UpdateVideoProgressRequest request,
        CancellationToken cancellationToken = default)
    {
        var duration = await PublishedVideosQuery()
            .Where(value => value.Id == videoId)
            .Select(value => value.DurationSeconds)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        if (!double.IsFinite(request.PositionSeconds) || request.PositionSeconds < 0 ||
            request.PositionSeconds > duration + _progressSettings.PositionToleranceSeconds)
        {
            throw ConflictException.Create(ErrorCodes.VideoProgressInvalid);
        }
        var position = Math.Min(request.PositionSeconds, duration);
        var isCompleted = position / duration >= _progressSettings.CompletionRatio ||
            duration - position <= _progressSettings.CompletionRemainingSeconds;
        await _progressStore.UpsertAsync(
            userId,
            videoId,
            position,
            isCompleted,
            _timeProvider.GetUtcNow(),
            cancellationToken);
    }

    /// <summary>
    /// 创建包含 rendition 和字幕的编辑者只读详情查询。
    /// </summary>
    private IQueryable<Video> EditorDetailsQuery()
        => _db.Videos.AsNoTracking()
            .Include(value => value.Renditions)
            .Include(value => value.Subtitles)
            .Include(value => value.CategoryAssignments)
                .ThenInclude(value => value.VideoCategory);

    /// <summary>
    /// 创建只包含可播放视频且媒体属性完整的登录用户查询。
    /// </summary>
    private IQueryable<Video> PublishedVideosQuery()
        => _db.Videos.AsNoTracking().Where(value =>
            value.ProcessingStatus == VideoProcessingStatus.Ready &&
            value.PublicationStatus == VideoPublicationStatus.Published &&
            value.DurationSeconds != null &&
            value.DisplayWidth != null &&
            value.DisplayHeight != null &&
            value.PublishedAt != null);

    /// <summary>
    /// 查找当前编辑者拥有的可变视频，不泄漏其他 owner 的实体存在性。
    /// </summary>
    private async Task<Video> FindOwnedVideoAsync(
        Guid videoId,
        Guid editorId,
        CancellationToken cancellationToken)
        => await _db.Videos.SingleOrDefaultAsync(
            value => value.Id == videoId && value.OwnerId == editorId,
            cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);

    /// <summary>
    /// 验证字幕对象是有界、无 BOM 的 UTF-8 文本且首行具有 WEBVTT header。
    /// </summary>
    private async Task ValidateWebVttAsync(
        MediaResource resource,
        CancellationToken cancellationToken)
    {
        var maxBytes = (long)_uploadSettings.SubtitleMaxMB * 1024 * 1024;
        var metadata = await _objectStorage.GetObjectMetadataAsync(
            resource.ObjectName,
            cancellationToken);
        if (metadata is null || metadata.Size <= 0 || metadata.Size > maxBytes)
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
        await using var content = new MemoryStream((int)metadata.Size);
        await _objectStorage.DownloadObjectAsync(
            resource.ObjectName,
            content,
            cancellationToken);
        if (content.Length != metadata.Size || content.Length > maxBytes)
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
        var bytes = content.ToArray();
        if (bytes.Length >= 3 && bytes[0] == 0xef && bytes[1] == 0xbb && bytes[2] == 0xbf)
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
        string text;
        try
        {
            text = new UTF8Encoding(false, true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
        using var reader = new StringReader(text);
        var firstLine = reader.ReadLine();
        if (firstLine is null || !firstLine.StartsWith("WEBVTT", StringComparison.Ordinal) ||
            (firstLine.Length > 6 && !char.IsWhiteSpace(firstLine[6])))
        {
            throw ConflictException.Create(ErrorCodes.VideoSubtitleInvalid);
        }
    }

    /// <summary>
    /// 保存视频并发变更并映射为稳定状态冲突。
    /// </summary>
    private async Task SaveWithVideoConflictAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
    }

    /// <summary>
    /// 将编辑实体映射为不包含内部对象路径的管理响应。
    /// </summary>
    private static EditorVideoResponse ToEditorResponse(Video video)
        => new(
            video.Id,
            video.SourceMediaResourceId,
            video.Title,
            video.Description,
            video.OriginalLanguage,
            video.ProcessingStatus,
            video.PublicationStatus,
            video.DurationSeconds,
            video.DisplayWidth,
            video.DisplayHeight,
            video.ContainerFormat,
            video.VideoCodec,
            video.AudioCodec,
            video.LastFailureCode,
            video.PublishedAt,
            video.Renditions.OrderBy(value => value.Height)
                .Select(value => new VideoRenditionResponse(
                    value.TargetHeight,
                    value.Width,
                    value.Height,
                    value.VideoBitrateKbps,
                    value.AudioBitrateKbps,
                    value.Codecs))
                .ToArray(),
            video.Subtitles.OrderBy(value => value.SortOrder)
                .Select(ToSubtitleResponse)
                .ToArray(),
            video.CategoryAssignments
                .OrderBy(value => value.VideoCategory.Name)
                .ThenBy(value => value.VideoCategoryId)
                .Select(value => new EditorVideoCategorySummaryResponse(
                    value.VideoCategory.Id,
                    value.VideoCategory.Name,
                    value.VideoCategory.Slug,
                    value.VideoCategory.IsActive))
                .ToArray(),
            video.CreatedAt,
            video.UpdatedAt);

    /// <summary>
    /// 将字幕实体映射为编辑者管理响应。
    /// </summary>
    private static EditorVideoSubtitleResponse ToSubtitleResponse(VideoSubtitle subtitle)
        => new(
            subtitle.Id,
            subtitle.MediaResourceId,
            subtitle.LanguageTag,
            subtitle.DisplayName,
            subtitle.IsDefault,
            subtitle.SortOrder);

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
    /// 一次加载并验证视频请求中的所有启用分类。
    /// </summary>
    private async Task<List<VideoCategory>> LoadUsableCategoriesAsync(
        IReadOnlyCollection<Guid> categoryIds,
        CancellationToken cancellationToken)
    {
        if (categoryIds is null ||
            categoryIds.Count > VideoConstraints.MaxCategoryCount ||
            categoryIds.Any(value => value == Guid.Empty))
        {
            throw new RequestValidationException(ErrorCodes.VideoCategoryIdsInvalid);
        }
        if (categoryIds.Count != categoryIds.Distinct().Count())
        {
            throw new RequestValidationException(ErrorCodes.VideoCategoryDuplicate);
        }
        if (categoryIds.Count == 0)
        {
            return [];
        }

        var categories = await _db.VideoCategories
            .Where(value => categoryIds.Contains(value.Id))
            .ToListAsync(cancellationToken);
        if (categories.Count != categoryIds.Count)
        {
            throw NotFoundException.Create(ErrorCodes.VideoCategoryNotFound);
        }
        if (categories.Any(value => !value.IsActive))
        {
            throw ConflictException.Create(ErrorCodes.VideoCategoryInactive);
        }
        return categories;
    }

    /// <summary>
    /// 按请求目标集合增删视频分类关联，不改变视频其他状态。
    /// </summary>
    private void SynchronizeCategories(
        Guid videoId,
        IReadOnlyCollection<VideoCategoryAssignment> existingAssignments,
        IReadOnlyCollection<VideoCategory> targetCategories)
    {
        var targetIds = targetCategories.Select(value => value.Id).ToHashSet();
        var existingIds = existingAssignments
            .Select(value => value.VideoCategoryId)
            .ToHashSet();
        _db.VideoCategoryAssignments.RemoveRange(existingAssignments
            .Where(value => !targetIds.Contains(value.VideoCategoryId)));
        foreach (var category in targetCategories.Where(value => !existingIds.Contains(value.Id)))
        {
            _db.VideoCategoryAssignments.Add(new VideoCategoryAssignment
            {
                VideoId = videoId,
                VideoCategoryId = category.Id
            });
        }
    }

    /// <summary>
    /// 以小写形式持久化已由 validator 验证的语言标签。
    /// </summary>
    private static string NormalizeLanguageTag(string value)
        => value.Trim().ToLowerInvariant();

    /// <summary>
    /// 返回包含末尾斜杠的对象目录前缀。
    /// </summary>
    private static string GetObjectDirectory(string objectName)
    {
        var separator = objectName.LastIndexOf('/');
        if (separator <= 0)
        {
            throw new InvalidOperationException("Video object has no protected directory.");
        }
        return objectName[..(separator + 1)];
    }
}
