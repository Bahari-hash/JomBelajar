using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TinyLang.Database;
using TinyLang.Endpoints;
using TinyLang.Infrastructure;
using TinyLang.Middlewares;
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

    builder.Services.AddDatabaseService(builder.Configuration);
    builder.Services.AddCacheService(builder.Configuration);
    builder.Services.AddMessageQueueService(builder.Configuration);

    builder.Services.AddTemplatesRenderingService();
    builder.Services.AddEmailSendingService();

    var app = builder.Build();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseExceptionHandler();
    app.UseRouting();

    app.UseAuthentication();
    app.UseAuthorization();

    app.MapGroup("/api")
        .MapBonusScenesApi();

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
