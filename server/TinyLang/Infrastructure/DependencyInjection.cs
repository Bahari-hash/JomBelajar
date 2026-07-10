using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using TinyLang.Constants;
using TinyLang.Entities.Enums;
using TinyLang.Settings;

namespace TinyLang.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddJwtAuthentication(
        this IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            var jwtSettings = configuration.GetSection(JwtSettings.SectionName).Get<JwtSettings>() ??
                throw new InvalidOperationException("Cannot get jwt settings from configuration");

            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = jwtSettings.Issuer,

                ValidateAudience = true,
                ValidAudience = jwtSettings.Audience,

                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,

                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(
                    Encoding.UTF8.GetBytes(jwtSettings.JwtSecret)),

                // Specify the name and role claim types
                // in order to make authorization service working
                NameClaimType = JwtClaimNamesExtension.Name,
                RoleClaimType = JwtClaimNamesExtension.Role,
            };
        });

        return services;
    }

    public static IServiceCollection AddAuthorizationPolicy(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            .AddPolicy(AuthorizationPolicies.RequireAdmin, policy =>
            {
                var minimumAdmin = new MinimumRoleRequirement(UserRole.Admin);
                policy.Requirements.Add(minimumAdmin);
            })
            .AddPolicy(AuthorizationPolicies.RequireEditor, policy =>
            {
                var minimumEditor = new MinimumRoleRequirement(UserRole.Editor);
                policy.Requirements.Add(minimumEditor);
            });

        return services;
    }

    public static IServiceCollection AddCacheService(
        this IServiceCollection services, IConfiguration configuration)
    {
        var redisConnection = configuration.GetConnectionString("RedisConnection") ??
            throw new InvalidOperationException("Cannot get redis connection string");
        services.AddStackExchangeRedisCache(options =>
        {
            options.Configuration = redisConnection;
            options.InstanceName = "tiny-lang";
        });

        return services;
    }
}
