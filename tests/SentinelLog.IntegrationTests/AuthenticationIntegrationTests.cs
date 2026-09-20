using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using SentinelLog.Application.DTOs.Auth;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;
using SentinelLog.Infrastructure.Data;
using Xunit;

namespace SentinelLog.IntegrationTests;

public class AuthenticationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public AuthenticationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidData_ShouldReturn201AndJwtToken()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var request = new RegisterRequest(
            Username: $"user_{unique}",
            Email: $"user_{unique}@sentinellog.local",
            Password: "SecurePassword123!#"
        );

        var response = await _client.PostAsJsonAsync("/api/v1/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrWhiteSpace();
        authResponse.TokenType.Should().Be("Bearer");
        authResponse.User.Username.Should().Be(request.Username);
        authResponse.User.Role.Should().Be(UserRole.Viewer.ToString());
    }

    [Fact]
    public async Task Register_WithDuplicateUsername_ShouldReturn409Conflict()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        var request = new RegisterRequest(
            Username: $"dup_{unique}",
            Email: $"first_{unique}@sentinellog.local",
            Password: "SecurePassword123!#"
        );

        var firstResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", request);
        firstResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        var dupRequest = new RegisterRequest(
            Username: $"dup_{unique}",
            Email: $"different_{unique}@sentinellog.local",
            Password: "SecurePassword123!#"
        );

        var dupResponse = await _client.PostAsJsonAsync("/api/v1/auth/register", dupRequest);
        dupResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

        var problem = await dupResponse.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Login_WithSeededAdminCredentials_ShouldReturn200AndValidToken()
    {
        var loginRequest = new LoginRequest("admin", "Admin123!#Sentinel");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var authResponse = await response.Content.ReadFromJsonAsync<AuthResponse>();

        authResponse.Should().NotBeNull();
        authResponse!.Token.Should().NotBeNullOrWhiteSpace();
        authResponse.User.Username.Should().Be("admin");
        authResponse.User.Role.Should().Be("Admin");
    }

    [Fact]
    public async Task Login_WithInvalidPassword_ShouldReturn401Unauthorized()
    {
        var loginRequest = new LoginRequest("admin", "WrongPassword!999");

        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithDeactivatedUser_ShouldReturn401Unauthorized()
    {
        // Create and deactivate a user in database
        var unique = Guid.NewGuid().ToString("N")[..8];
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<SentinelLogDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<SentinelLog.Application.Interfaces.IPasswordHasher>();

            db.Users.Add(new User
            {
                Username = $"deactivated_{unique}",
                Email = $"deactivated_{unique}@sentinellog.local",
                PasswordHash = hasher.Hash("ValidPassword123!#"),
                Role = UserRole.Viewer,
                IsActive = false,
                CreatedAt = DateTimeOffset.UtcNow
            });
            await db.SaveChangesAsync();
        }

        var loginRequest = new LoginRequest($"deactivated_{unique}", "ValidPassword123!#");
        var response = await _client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Detail.Should().Contain("deactivated");
    }
}
