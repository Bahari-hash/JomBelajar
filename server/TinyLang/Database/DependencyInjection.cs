using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyLang.Interfaces;

namespace TinyLang.Database;

public static class DependencyInjection
{
    public static IServiceCollection AddDatabaseService(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AuditableEntityInterceptor>();

        var databaseConnection = configuration.GetConnectionString("DatabaseConnection") ??
            throw new InvalidOperationException("Cannot get database connection string");
        services.AddDbContext<IApplicationDbContext, ApplicationDbContext>((sp, options) =>
        {
            var auditableEntityInterceptor = sp.GetRequiredService<AuditableEntityInterceptor>();
            options.UseNpgsql(databaseConnection).AddInterceptors(auditableEntityInterceptor);
        });

        return services;
    }
}
