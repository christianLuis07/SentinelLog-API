using Microsoft.EntityFrameworkCore;
using SentinelLog.Api.Extensions;
using SentinelLog.Api.Middleware;
using SentinelLog.Application.Interfaces;
using SentinelLog.Infrastructure.Data;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog structured logging
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File("logs/sentinellog-.log", rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] [{CorrelationId}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddDatabase(builder.Configuration);
builder.Services.AddSecurityAndAuthentication(builder.Configuration);
builder.Services.AddApplicationServices();
builder.Services.AddCustomRateLimiting(builder.Configuration);
builder.Services.AddSwaggerDocumentation();
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseMiddleware<SecurityHeadersMiddleware>();
app.UseMiddleware<ExceptionHandlingMiddleware>();

app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "HTTP {RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000} ms (CorrelationId: {CorrelationId})";
    options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
    {
        diagnosticContext.Set("CorrelationId", httpContext.Items["CorrelationId"] ?? "unknown");
        diagnosticContext.Set("ClientIP", httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown");
        if (httpContext.User.Identity?.IsAuthenticated == true)
        {
            diagnosticContext.Set("UserId", httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value);
            diagnosticContext.Set("Username", httpContext.User.Identity.Name);
        }
    };
});

// Enable Swagger in all environments for API portfolio exploration
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "SentinelLog API v1");
    c.RoutePrefix = "swagger";
    c.DocumentTitle = "SentinelLog API Documentation";
});

app.UseRouting();

app.UseRateLimiter();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

// Root redirection or welcome info
app.MapGet("/", () => Results.Ok(new
{
    service = "SentinelLog API",
    version = "v1",
    status = "Healthy",
    documentation = "/swagger",
    health = "/health"
}));

// Apply migrations and seed development accounts on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<ILogger<Program>>();
    var dbContext = services.GetRequiredService<SentinelLogDbContext>();
    var passwordHasher = services.GetRequiredService<IPasswordHasher>();

    try
    {
        if (dbContext.Database.IsRelational())
        {
            logger.LogInformation("Applying pending database migrations...");
            await dbContext.Database.MigrateAsync();
        }

        await DbInitializer.SeedAsync(dbContext, passwordHasher, logger);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "An error occurred during database migration/seeding on startup.");
    }
}

try
{
    Log.Information("Starting SentinelLog API host...");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Host terminated unexpectedly.");
}
finally
{
    Log.CloseAndFlush();
}

public partial class Program { }
