using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SentinelLog.Application.DTOs.Auth;
using Xunit;

namespace SentinelLog.IntegrationTests;

public class SecurityIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public SecurityIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Response_ShouldEchoProvidedCorrelationIdHeader()
    {
        var client = _factory.CreateClient();
        var customCorrelationId = "sentinel-trace-" + Guid.NewGuid().ToString("N");

        var request = new HttpRequestMessage(HttpMethod.Get, "/health");
        request.Headers.Add("X-Correlation-ID", customCorrelationId);

        var response = await client.SendAsync(request);

        response.Headers.Should().ContainKey("X-Correlation-ID");
        response.Headers.GetValues("X-Correlation-ID").First().Should().Be(customCorrelationId);
    }

    [Fact]
    public async Task Response_ShouldGenerateCorrelationIdWhenNotProvided()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Correlation-ID");
        var generatedId = response.Headers.GetValues("X-Correlation-ID").First();
        generatedId.Should().NotBeNullOrWhiteSpace();
        Guid.TryParse(generatedId, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Response_ShouldIncludeHardenedSecurityHeaders()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.Headers.Should().ContainKey("X-Content-Type-Options");
        response.Headers.GetValues("X-Content-Type-Options").First().Should().Be("nosniff");

        response.Headers.Should().ContainKey("X-Frame-Options");
        response.Headers.GetValues("X-Frame-Options").First().Should().Be("DENY");

        response.Headers.Should().ContainKey("Referrer-Policy");
        response.Headers.GetValues("Referrer-Policy").First().Should().Be("strict-origin-when-cross-origin");
    }

    [Fact]
    public async Task LoginEndpoint_WhenExceedingRateLimit_ShouldReturn429TooManyRequests()
    {
        // Rate limit for auth-login is 5 requests per minute
        var client = _factory.CreateClient();
        var loginRequest = new LoginRequest("admin", "Admin123!#Sentinel");

        HttpResponseMessage? lastResponse = null;
        var got429 = false;

        // Perform 8 rapid requests from same client IP
        for (int i = 0; i < 8; i++)
        {
            lastResponse = await client.PostAsJsonAsync("/api/v1/auth/login", loginRequest);
            if (lastResponse.StatusCode == HttpStatusCode.TooManyRequests)
            {
                got429 = true;
                break;
            }
        }

        got429.Should().BeTrue("Sending requests exceeding the rate limit window must result in HTTP 429 Too Many Requests.");
    }
}
