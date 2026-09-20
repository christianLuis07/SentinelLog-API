using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Analytics;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Domain.Enums;
using Xunit;

namespace SentinelLog.IntegrationTests;

public class EventsIntegrationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly CustomWebApplicationFactory _factory;

    public EventsIntegrationTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task IngestEvent_WithValidData_ShouldReturn201CreatedAndLocationHeader()
    {
        var client = _factory.CreateClientWithRole(UserRole.Service, "collector-agent");

        var eventRequest = new CreateSecurityEventRequest(
            EventType: EventType.UnauthorizedAccessAttempt,
            Severity: EventSeverity.High,
            Source: "api-gateway",
            ActorId: null,
            IpAddress: "203.0.113.42",
            UserAgent: "SecurityScanner/1.0",
            Resource: "/api/v1/internal/config",
            Description: "Probing internal configuration endpoints",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: Guid.NewGuid().ToString("D"),
            Metadata: new JsonObject
            {
                ["attackPattern"] = "directory_traversal",
                ["blocked"] = true
            }
        );

        var response = await client.PostAsJsonAsync("/api/v1/events", eventRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        response.Headers.Location.Should().NotBeNull();

        var created = await response.Content.ReadFromJsonAsync<SecurityEventResponse>();
        created.Should().NotBeNull();
        created!.Id.Should().BeGreaterThan(0);
        created.EventType.Should().Be("UnauthorizedAccessAttempt");
        created.Severity.Should().Be("High");
        created.Metadata.Should().NotBeNull();
        created.Metadata!["attackPattern"]!.ToString().Should().Be("directory_traversal");
    }

    [Fact]
    public async Task IngestEvent_WithInvalidData_ShouldReturn400BadRequest()
    {
        var client = _factory.CreateClientWithRole(UserRole.Service);

        var badRequest = new CreateSecurityEventRequest(
            EventType: (EventType)999, // Invalid enum value
            Severity: (EventSeverity)999,
            Source: "", // Empty source
            ActorId: null,
            IpAddress: "invalid-ip",
            UserAgent: null,
            Resource: "",
            Description: "",
            Timestamp: null,
            CorrelationId: null,
            Metadata: null
        );

        var response = await client.PostAsJsonAsync("/api/v1/events", badRequest);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problem.Should().NotBeNull();
        problem!.Status.Should().Be((int)HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task GetEvents_WithFilterAndSearch_ShouldReturnMatchingResults()
    {
        var client = _factory.CreateClientWithRole(UserRole.SecurityAnalyst);

        // Ingest unique event to search for
        var uniqueTerm = "sqli_" + Guid.NewGuid().ToString("N")[..6];
        var eventRequest = new CreateSecurityEventRequest(
            EventType: EventType.SuspiciousActivity,
            Severity: EventSeverity.Critical,
            Source: "waf-service",
            ActorId: null,
            IpAddress: "198.51.100.22",
            UserAgent: "Mozilla/5.0",
            Resource: $"/api/v1/data?query={uniqueTerm}",
            Description: $"Detected SQL injection payload with term {uniqueTerm}",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: null,
            Metadata: null
        );

        var ingestResponse = await client.PostAsJsonAsync("/api/v1/events", eventRequest);
        ingestResponse.StatusCode.Should().Be(HttpStatusCode.Created);

        // Search for the unique term
        var searchUrl = $"/api/v1/events?search={uniqueTerm}&severity=Critical&page=1&pageSize=10";
        var searchResponse = await client.GetAsync(searchUrl);

        searchResponse.StatusCode.Should().Be(HttpStatusCode.OK);
        var pagedResult = await searchResponse.Content.ReadFromJsonAsync<PagedResult<SecurityEventResponse>>();

        pagedResult.Should().NotBeNull();
        pagedResult!.Data.Should().ContainSingle(e => e.Description.Contains(uniqueTerm));
    }

    [Fact]
    public async Task GetStatisticsAndTimeline_ShouldReturn200AndValidStructure()
    {
        var client = _factory.CreateClientWithRole(UserRole.Viewer);

        var statsResponse = await client.GetAsync("/api/v1/events/statistics");
        statsResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var stats = await statsResponse.Content.ReadFromJsonAsync<SecurityEventStatisticsResponse>();
        stats.Should().NotBeNull();
        stats!.TotalEvents.Should().BeGreaterThanOrEqualTo(0);

        var timelineResponse = await client.GetAsync("/api/v1/events/timeline");
        timelineResponse.StatusCode.Should().Be(HttpStatusCode.OK);

        var timeline = await timelineResponse.Content.ReadFromJsonAsync<IReadOnlyList<DailyTimelineItemResponse>>();
        timeline.Should().NotBeNull();
    }
}
