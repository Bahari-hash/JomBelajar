using Microsoft.Extensions.DependencyInjection;
using TinyLang.Policies;

namespace TinyLang.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddSingleton<MediaUploadPolicy>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserSessionService, UserSessionService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAccountSecurityService, AccountSecurityService>();
        services.AddScoped<IMediaResourceService, MediaResourceService>();
        services.AddScoped<IArticleService, ArticleService>();
        services.AddScoped<IArticleCategoryService, ArticleCategoryService>();

        return services;
    }
}
