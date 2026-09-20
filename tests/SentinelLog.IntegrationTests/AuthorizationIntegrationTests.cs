using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Domain.Enums;
using Xunit;

namespace SentinelLog.IntegrationTests;

public class AuthorizationIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public AuthorizationIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task AdminUser_AccessingUsersEndpoint_ShouldSucceed()
    {
        var client = _factory.CreateClientWithRole(UserRole.Admin);

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ViewerUser_AccessingUsersEndpoint_ShouldBeForbidden()
    {
        var client = _factory.CreateClientWithRole(UserRole.Viewer);

        var response = await client.GetAsync("/api/v1/users");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ServiceRole_AccessingAuditLogs_ShouldBeForbidden()
    {
        var client = _factory.CreateClientWithRole(UserRole.Service);

        var response = await client.GetAsync("/api/v1/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task SecurityAnalyst_AccessingAuditLogs_ShouldSucceed()
    {
        var client = _factory.CreateClientWithRole(UserRole.SecurityAnalyst);

        var response = await client.GetAsync("/api/v1/audit-logs");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Viewer_SubmittingSecurityEvent_ShouldBeForbidden()
    {
        var client = _factory.CreateClientWithRole(UserRole.Viewer);
        var eventRequest = new CreateSecurityEventRequest(
            EventType: EventType.SuspiciousActivity,
            Severity: EventSeverity.Medium,
            Source: "client-app",
            ActorId: null,
            IpAddress: "127.0.0.1",
            UserAgent: "Mozilla/5.0",
            Resource: "/api/test",
            Description: "Testing viewer ingestion restriction",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: null,
            Metadata: null
        );

        var response = await client.PostAsJsonAsync("/api/v1/events", eventRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task AnonymousRequest_ToProtectedResource_ShouldBeUnauthorized()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/v1/events");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.Unauthorized);
    }
}
