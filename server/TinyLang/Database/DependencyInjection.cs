using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using TinyLang.Interfaces;

namespace TinyLang.Database;

/// <summary>
/// 提供 PostgreSQL DbContext 和审计拦截器的注册入口。
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// 使用应用连接串注册数据库上下文及审计拦截器。
    /// </summary>
    /// <param name="services">应用服务集合。</param>
    /// <param name="configuration">数据库连接配置源。</param>
    /// <returns>完成注册后的同一服务集合。</returns>
    /// <exception cref="InvalidOperationException">数据库连接串缺失。</exception>
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
