using System.Net.Http.Headers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using SentinelLog.Api;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;

namespace SentinelLog.IntegrationTests;

public class CustomWebApplicationFactory : WebApplicationFactory<ApiMarker>
{
    private static readonly string TestDbName = "TestDb_" + Guid.NewGuid().ToString("N");

    static CustomWebApplicationFactory()
    {
        Environment.SetEnvironmentVariable("USE_IN_MEMORY_DB", "true");
        Environment.SetEnvironmentVariable("IN_MEMORY_DB_NAME", TestDbName);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", "InMemory");
        Environment.SetEnvironmentVariable("RateLimiting__LoginPermitLimit", "5");
        Environment.SetEnvironmentVariable("RateLimiting__RegisterPermitLimit", "10");
        Environment.SetEnvironmentVariable("RateLimiting__EventsPermitLimit", "60");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
    }

    public HttpClient CreateClientWithRole(UserRole role, string username = "testuser")
    {
        var client = CreateClient();

        using var scope = Services.CreateScope();
        var jwtGenerator = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>();

        var user = new User
        {
            Id = 999 + (int)role,
            Username = $"{username}_{role}",
            Email = $"{username}_{role}@test.local",
            Role = role,
            IsActive = true
        };

        var (token, _) = jwtGenerator.GenerateToken(user);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }
}
