using Microsoft.Extensions.DependencyInjection;
using TinyLang.Policies;
using TinyLang.Workers;

namespace TinyLang.Services;

/// <summary>
/// 提供业务服务和媒体上传策略的依赖注入注册入口。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 注册 TinyLang 业务服务及其生命周期。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<MediaUploadPolicy>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAccountSecurityService, AccountSecurityService>();
        services.AddScoped<IMediaResourceService, MediaResourceService>();
        services.AddScoped<IMediaUploadMaintenanceService, MediaUploadMaintenanceService>();
        services.AddHostedService<MediaUploadMaintenanceWorker>();
        services.AddScoped<IArticleService, ArticleService>();
        services.AddScoped<IArticleCategoryService, ArticleCategoryService>();
        services.AddScoped<IVideoCategoryService, VideoCategoryService>();
        services.AddScoped<IVideoService, VideoService>();
        services.AddScoped<IAudioClipService, AudioClipService>();
        services.AddScoped<IWordService, WordService>();
        services.AddScoped<IWordStudyService, WordStudyService>();
        services.AddScoped<IPaperService, PaperService>();
        services.AddScoped<IPaperAttemptService, PaperAttemptService>();
        services.AddSingleton<VideoRenditionPlanner>();
        services.AddScoped<IVideoProcessingService, VideoProcessingService>();
        services.AddScoped<IVideoProcessingDispatcher, VideoProcessingDispatcher>();
        services.AddHostedService<VideoProcessingDispatchWorker>();
        services.AddScoped<IAudioProcessingService, AudioProcessingService>();
        services.AddScoped<IAudioProcessingDispatcher, AudioProcessingDispatcher>();
        services.AddHostedService<AudioProcessingDispatchWorker>();
        return services;
    }
}
