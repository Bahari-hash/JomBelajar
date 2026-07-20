using Microsoft.Extensions.DependencyInjection;

namespace TinyLang.Services;

public static class DependencyInjection
{
    public static IServiceCollection AddBusinessServices(this IServiceCollection services)
    {
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IAccountSecurityService, AccountSecurityService>();

        return services;
    }
}
