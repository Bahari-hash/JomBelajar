using FluentValidation;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TinyLang.Database;
using TinyLang.Endpoints;
using TinyLang.Filters;
using TinyLang.Infrastructure;
using TinyLang.Middlewares;
using TinyLang.Services;
using TinyLang.Settings;

Log.Logger = new LoggerConfiguration()
   .WriteTo.Console()
   .CreateBootstrapLogger();

try
{
    Log.Information("Server starting...");

    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((context, services, configuration) =>
        configuration
            .ReadFrom.Configuration(context.Configuration)
            .ReadFrom.Services(services)
            .Enrich.FromLogContext());
    builder.Services.AddOpenApi();

    builder.Services.AddProblemDetails();
    builder.Services.AddExceptionHandler<GlobalExceptionHandler>();

    builder.Services.AddAppSettings(builder.Configuration);

    builder.Services.AddJwtAuthentication(builder.Configuration);
    builder.Services.AddAuthorizationPolicy();
    builder.Services.AddCustomRateLimiter(builder.Configuration);

    builder.Services.AddDatabaseService(builder.Configuration);
    builder.Services.AddCacheService(builder.Configuration);
    builder.Services.AddMessageQueueService(builder.Configuration);

    builder.Services.AddTemplatesRenderingService();
    builder.Services.AddEmailSendingService();
    builder.Services.AddObjectStorageService();

    builder.Services.AddVerificationCodeService();
    builder.Services.AddSecureService();

    builder.Services.AddValidatorsFromAssemblyContaining<Program>();
    builder.Services.AddBusinessServices();

    var app = builder.Build();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseForwardedHeaders();
    app.UseHttpsRedirection();
    app.UseExceptionHandler();
    app.UseRouting();

    app.UseAuthentication();
    app.UseRateLimiter();
    app.UseAuthorization();

    app.MapGroup("/api")
        .MapAuthApi()
        .MapUsersApi()
        .MapSecurityApi()
        .MapUploadsApi()
        .MapArticlesApi()
        .MapBonusScenesApi()
        .AddEndpointFilter<ValidationEndpointFilter>();

    app.Run();
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // HostAbortedException usually used in design time.
    // For example, EntityFramework Core migrations.
    Log.Fatal(ex, "Server terminated unexpectedly!");
}
finally
{
    Log.CloseAndFlush();
}
