using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using TinyLang.Endpoints;

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

    var app = builder.Build();
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
    }

    app.UseHttpsRedirection();
    app.UseRouting();

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
