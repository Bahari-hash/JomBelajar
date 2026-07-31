using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using TinyLang.Dtos;
using TinyLang.Entities;
using TinyLang.Entities.Enums;
using TinyLang.Exceptions;
using TinyLang.Interfaces;
using TinyLang.Settings;

namespace TinyLang.Services;

/// <summary>
/// 实现视频源绑定、状态机、登录播放授权和播放进度业务规则。
/// </summary>
public sealed class VideoService : IVideoService
{
    private readonly IApplicationDbContext _db;
    private readonly IVideoDeliveryUrlService _deliveryUrlService;
    private readonly IUserVideoProgressStore _progressStore;
    private readonly IDatabaseExceptionClassifier _databaseExceptionClassifier;
    private readonly VideoProgressSettings _progressSettings;
    private readonly TimeProvider _timeProvider;

    /// <summary>
    /// 使用数据库、delivery、进度持久化和业务配置创建视频服务。
    /// </summary>
    public VideoService(
        IApplicationDbContext db,
        IVideoDeliveryUrlService deliveryUrlService,
        IUserVideoProgressStore progressStore,
        IDatabaseExceptionClassifier databaseExceptionClassifier,
        IOptions<VideoProgressSettings> progressOptions,
        TimeProvider timeProvider)
    {
        _db = db;
        _deliveryUrlService = deliveryUrlService;
        _progressStore = progressStore;
        _databaseExceptionClassifier = databaseExceptionClassifier;
        _progressSettings = progressOptions.Value;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> CreateAsync(
        Guid adminId,
        CreateVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        var source = await _db.MediaResources.SingleOrDefaultAsync(
            value => value.Id == request.SourceMediaResourceId,
            cancellationToken);
        if (source is null || source.Module != ResourceModule.CourseVideo)
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
            CreatedById = adminId,
            LastEditorId = adminId,
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
        return await GetAdminByIdAsync(video.Id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResponse<AdminVideoListItemResponse>> GetAdminListAsync(
        AdminVideoListRequest request,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Videos.AsNoTracking();
        if (request.PublicationStatus is null)
        {
            query = query.Where(value =>
                value.PublicationStatus != VideoPublicationStatus.Archived);
        }
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
        if (request.CreatedById is { } createdById)
        {
            query = query.Where(value => value.CreatedById == createdById);
        }

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .OrderByDescending(value => value.UpdatedAt)
            .ThenByDescending(value => value.Id)
            .Skip((request.Page - 1) * request.PageSize)
            .Take(request.PageSize)
            .Select(value => new AdminVideoListItemResponse(
                value.Id,
                value.Title,
                value.OriginalLanguage,
                value.ProcessingStatus,
                value.PublicationStatus,
                value.DurationSeconds,
                value.LastFailureCode,
                value.CategoryAssignments
                    .OrderBy(assignment => assignment.VideoCategory.Name)
                    .ThenBy(assignment => assignment.VideoCategoryId)
                    .Select(assignment => new AdminVideoCategorySummaryResponse(
                        assignment.VideoCategory.Id,
                        assignment.VideoCategory.Name,
                        assignment.VideoCategory.Slug,
                        assignment.VideoCategory.IsActive))
                    .ToList(),
                new ContentAuditUserResponse(
                    value.CreatedById,
                    value.CreatedBy.Nickname,
                    value.CreatedBy.AvatarUrl),
                new ContentAuditUserResponse(
                    value.LastEditorId,
                    value.LastEditor.Nickname,
                    value.LastEditor.AvatarUrl),
                value.ConcurrencyStamp,
                value.CreatedAt,
                value.UpdatedAt,
                value.ProcessingJobs
                    .OrderByDescending(job => job.CreatedAt)
                    .ThenByDescending(job => job.Id)
                    .Select(job => new VideoProcessingJobSummaryResponse(
                        job.Id,
                        job.Status,
                        job.AttemptCount,
                        job.NextAttemptAt,
                        job.StartedAt,
                        job.CompletedAt,
                        job.FailureCode))
                    .FirstOrDefault()))
            .ToListAsync(cancellationToken);
        return CreatePage(items, request.Page, request.PageSize, totalCount);
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> GetAdminByIdAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await AdminDetailsQuery()
            .SingleOrDefaultAsync(value => value.Id == videoId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        var auditUsers = await _db.Users.AsNoTracking()
            .Where(value => value.Id == video.CreatedById || value.Id == video.LastEditorId)
            .ToListAsync(cancellationToken);
        return ToAdminResponse(video, auditUsers);
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> UpdateAsync(
        Guid videoId,
        Guid adminId,
        UpdateVideoRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(videoId, cancellationToken);
        EnsureExpectedStamp(video, request.ConcurrencyStamp);
        if (video.PublicationStatus is VideoPublicationStatus.Published or
            VideoPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
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
        video.LastEditorId = adminId;
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetAdminByIdAsync(videoId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> PublishAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(videoId, cancellationToken);
        EnsureExpectedStamp(video, request.ConcurrencyStamp);
        if (video.PublicationStatus == VideoPublicationStatus.Published)
        {
            return await GetAdminByIdAsync(videoId, cancellationToken);
        }
        if (video.PublicationStatus == VideoPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
        if (video.ProcessingStatus != VideoProcessingStatus.Ready)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
        video.PublicationStatus = VideoPublicationStatus.Published;
        video.PublishedAt = _timeProvider.GetUtcNow();
        video.LastEditorId = adminId;
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetAdminByIdAsync(videoId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> UnpublishAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(videoId, cancellationToken);
        EnsureExpectedStamp(video, request.ConcurrencyStamp);
        if (video.PublicationStatus == VideoPublicationStatus.Unpublished)
        {
            return await GetAdminByIdAsync(videoId, cancellationToken);
        }
        if (video.PublicationStatus != VideoPublicationStatus.Published)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
        video.PublicationStatus = VideoPublicationStatus.Unpublished;
        video.LastEditorId = adminId;
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetAdminByIdAsync(videoId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> RetryAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(videoId, cancellationToken);
        EnsureExpectedStamp(video, request.ConcurrencyStamp);
        if (video.PublicationStatus == VideoPublicationStatus.Archived)
        {
            throw ConflictException.Create(ErrorCodes.VideoStatusConflict);
        }
        if (video.ProcessingStatus != VideoProcessingStatus.Failed)
        {
            throw ConflictException.Create(ErrorCodes.VideoRetryConflict);
        }
        video.ProcessingStatus = VideoProcessingStatus.Queued;
        video.LastFailureCode = null;
        video.LastEditorId = adminId;
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
            throw ConflictException.Create(ErrorCodes.VideoConcurrencyConflict);
        }
        return await GetAdminByIdAsync(videoId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<AdminVideoResponse> ArchiveAsync(
        Guid videoId,
        Guid adminId,
        VideoMutationRequest request,
        CancellationToken cancellationToken = default)
    {
        var video = await FindVideoAsync(videoId, cancellationToken);
        EnsureExpectedStamp(video, request.ConcurrencyStamp);
        if (video.PublicationStatus == VideoPublicationStatus.Archived)
        {
            return await GetAdminByIdAsync(videoId, cancellationToken);
        }
        if (video.PublicationStatus == VideoPublicationStatus.Published ||
            await _db.VideoProcessingJobs.AsNoTracking().AnyAsync(
                job => job.VideoId == videoId &&
                    (job.Status == VideoProcessingJobStatus.Queued ||
                        job.Status == VideoProcessingJobStatus.Processing),
                cancellationToken))
        {
            throw ConflictException.Create(ErrorCodes.VideoArchiveConflict);
        }

        video.PublicationStatus = VideoPublicationStatus.Archived;
        video.ArchivedAt = _timeProvider.GetUtcNow();
        video.LastEditorId = adminId;
        video.ConcurrencyStamp = Guid.NewGuid();
        await SaveWithVideoConflictAsync(cancellationToken);
        return await GetAdminByIdAsync(videoId, cancellationToken);
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
            .SingleOrDefaultAsync(value => value.Id == videoId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        if (video.MasterPlaylistObjectName is null ||
            video.CurrentOutputVersion is null ||
            video.DurationSeconds is null)
        {
            throw NotFoundException.Create(ErrorCodes.VideoNotFound);
        }

        var progress = await _db.UserVideoProgress.AsNoTracking()
            .SingleOrDefaultAsync(
                value => value.UserId == userId && value.VideoId == videoId,
                cancellationToken);
        return CreatePlaybackResponse(
            video,
            progress?.PositionSeconds ?? 0,
            progress?.IsCompleted ?? false);
    }

    /// <inheritdoc />
    public async Task<VideoPlaybackResponse> GetAdminPlaybackAsync(
        Guid videoId,
        CancellationToken cancellationToken = default)
    {
        var video = await _db.Videos.AsNoTracking()
            .SingleOrDefaultAsync(value =>
                value.Id == videoId &&
                value.ProcessingStatus == VideoProcessingStatus.Ready &&
                value.MasterPlaylistObjectName != null &&
                value.CurrentOutputVersion != null &&
                value.DurationSeconds != null,
                cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);

        return CreatePlaybackResponse(video, 0, false);
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
    /// 创建包含 rendition 和分类的管理员只读详情查询。
    /// </summary>
    private IQueryable<Video> AdminDetailsQuery()
        => _db.Videos.AsNoTracking()
            .Include(value => value.Renditions)
            .Include(value => value.ProcessingJobs)
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
    /// 为已经通过播放产物校验的视频生成短期地址和指定进度响应。
    /// </summary>
    private VideoPlaybackResponse CreatePlaybackResponse(
        Video video,
        double positionSeconds,
        bool isCompleted)
    {
        var masterObjectName = video.MasterPlaylistObjectName
            ?? throw new InvalidOperationException("Video master playlist is missing.");
        var durationSeconds = video.DurationSeconds
            ?? throw new InvalidOperationException("Video duration is missing.");
        var outputPrefix = GetObjectDirectory(masterObjectName);
        var masterUrl = _deliveryUrlService.CreateUrl(
            masterObjectName,
            outputPrefix);
        var posterUrl = video.PosterObjectName is null
            ? null
            : _deliveryUrlService.CreateUrl(video.PosterObjectName, outputPrefix).Url;
        return new VideoPlaybackResponse(
            masterUrl.Url,
            posterUrl,
            masterUrl.ExpiresAt,
            durationSeconds,
            positionSeconds,
            isCompleted);
    }

    /// <summary>
    /// 查找管理员可变更的全局视频。
    /// </summary>
    private async Task<Video> FindVideoAsync(
        Guid videoId,
        CancellationToken cancellationToken)
        => await _db.Videos.SingleOrDefaultAsync(value => value.Id == videoId, cancellationToken)
            ?? throw NotFoundException.Create(ErrorCodes.VideoNotFound);

    /// <summary>
    /// 在任何状态判断前校验客户端读取到的视频版本。
    /// </summary>
    private static void EnsureExpectedStamp(Video video, Guid expectedStamp)
    {
        if (video.ConcurrencyStamp != expectedStamp)
        {
            throw ConflictException.Create(ErrorCodes.VideoConcurrencyConflict);
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
            throw ConflictException.Create(ErrorCodes.VideoConcurrencyConflict);
        }
    }

    /// <summary>
    /// 将编辑实体映射为不包含内部对象路径的管理响应。
    /// </summary>
    private static AdminVideoResponse ToAdminResponse(
        Video video,
        IReadOnlyCollection<User> auditUsers)
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
            video.ArchivedAt,
            video.Renditions.OrderBy(value => value.Height)
                .Select(value => new VideoRenditionResponse(
                    value.TargetHeight,
                    value.Width,
                    value.Height,
                    value.VideoBitrateKbps,
                    value.AudioBitrateKbps,
                    value.Codecs))
                .ToArray(),
            video.CategoryAssignments
                .OrderBy(value => value.VideoCategory.Name)
                .ThenBy(value => value.VideoCategoryId)
                .Select(value => new AdminVideoCategorySummaryResponse(
                    value.VideoCategory.Id,
                    value.VideoCategory.Name,
                    value.VideoCategory.Slug,
                    value.VideoCategory.IsActive))
                .ToArray(),
            ToAuditUser(video.CreatedById, auditUsers),
            ToAuditUser(video.LastEditorId, auditUsers),
            video.ConcurrencyStamp,
            video.CreatedAt,
            video.UpdatedAt,
            video.ProcessingJobs
                .OrderByDescending(value => value.CreatedAt)
                .ThenByDescending(value => value.Id)
                .Select(ToJobSummary)
                .FirstOrDefault());

    /// <summary>
    /// 将用户映射为不含账号、邮箱或角色的内容审计摘要。
    /// </summary>
    private static ContentAuditUserResponse ToAuditUser(
        Guid userId,
        IEnumerable<User> auditUsers)
    {
        var user = auditUsers.FirstOrDefault(value => value.Id == userId);
        return new(userId, user?.Nickname, user?.AvatarUrl);
    }

    /// <summary>
    /// 将处理任务映射为不含租约和基础设施字段的管理摘要。
    /// </summary>
    private static VideoProcessingJobSummaryResponse ToJobSummary(VideoProcessingJob job)
        => new(
            job.Id,
            job.Status,
            job.AttemptCount,
            job.NextAttemptAt,
            job.StartedAt,
            job.CompletedAt,
            job.FailureCode);

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
