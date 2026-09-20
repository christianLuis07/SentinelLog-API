using System.Text.Json.Nodes;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;
using SentinelLog.Infrastructure.Data;
using SentinelLog.Infrastructure.Services;
using Xunit;

namespace SentinelLog.UnitTests;

public class SecurityEventServiceTests
{
    private SentinelLogDbContext CreateInMemoryDbContext()
    {
        var options = new DbContextOptionsBuilder<SentinelLogDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new SentinelLogDbContext(options);
    }

    [Fact]
    public async Task CreateEventAsync_ShouldPersistEventAndReturnValidResponse()
    {
        using var db = CreateInMemoryDbContext();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        mockCurrentUser.Setup(c => c.CorrelationId).Returns("corr-123");
        var mockAudit = new Mock<IAuditLogService>();

        var service = new SecurityEventService(db, mockCurrentUser.Object, mockAudit.Object);

        var request = new CreateSecurityEventRequest(
            EventType: EventType.LoginFailure,
            Severity: EventSeverity.Medium,
            Source: "web-gateway",
            ActorId: null,
            IpAddress: "10.0.0.5",
            UserAgent: "Mozilla/5.0",
            Resource: "/api/v1/auth/login",
            Description: "Invalid password supplied",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: null,
            Metadata: new JsonObject { ["reason"] = "bad_password" }
        );

        var result = await service.CreateEventAsync(request);

        result.Should().NotBeNull();
        result.Id.Should().BeGreaterThan(0);
        result.EventType.Should().Be("LoginFailure");
        result.Severity.Should().Be("Medium");
        result.CorrelationId.Should().Be("corr-123");
        result.Metadata.Should().NotBeNull();
        result.Metadata!["reason"]!.ToString().Should().Be("bad_password");

        var dbEntity = await db.SecurityEvents.FirstOrDefaultAsync(e => e.Id == result.Id);
        dbEntity.Should().NotBeNull();
        dbEntity!.IPAddress.Should().Be("10.0.0.5");
    }

    [Fact]
    public async Task GetEventsAsync_WithFilteringAndPagination_ShouldReturnExpectedSubset()
    {
        using var db = CreateInMemoryDbContext();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var mockAudit = new Mock<IAuditLogService>();

        // Seed 10 events with varying severity
        for (int i = 1; i <= 10; i++)
        {
            db.SecurityEvents.Add(new SecurityEvent
            {
                EventType = i % 2 == 0 ? EventType.LoginSuccess : EventType.LoginFailure,
                Severity = i <= 3 ? EventSeverity.Critical : (i <= 6 ? EventSeverity.High : EventSeverity.Low),
                Source = "test-service",
                IPAddress = $"192.168.1.{i}",
                Resource = "/auth",
                Description = $"Event number {i}",
                Timestamp = DateTimeOffset.UtcNow.AddMinutes(-i),
                CorrelationId = $"corr-{i}",
                CreatedAt = DateTimeOffset.UtcNow
            });
        }
        await db.SaveChangesAsync();

        var service = new SecurityEventService(db, mockCurrentUser.Object, mockAudit.Object);

        var query = new SecurityEventQueryParams
        {
            Page = 1,
            PageSize = 5,
            Severity = EventSeverity.Critical
        };

        var result = await service.GetEventsAsync(query);

        result.Data.Should().HaveCount(3);
        result.TotalItems.Should().Be(3);
        result.Data.Should().OnlyContain(e => e.Severity == "Critical");
    }

    [Fact]
    public async Task GetStatisticsAsync_ShouldComputeAccurateAggregations()
    {
        using var db = CreateInMemoryDbContext();
        var mockCurrentUser = new Mock<ICurrentUserService>();
        var mockAudit = new Mock<IAuditLogService>();

        db.SecurityEvents.AddRange(
            new SecurityEvent
            {
                EventType = EventType.LoginFailure,
                Severity = EventSeverity.High,
                Source = "auth",
                IPAddress = "1.1.1.1",
                Resource = "/login",
                Description = "Failed attempt 1",
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = "1"
            },
            new SecurityEvent
            {
                EventType = EventType.LoginFailure,
                Severity = EventSeverity.Medium,
                Source = "auth",
                IPAddress = "1.1.1.2",
                Resource = "/login",
                Description = "Failed attempt 2",
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = "2"
            },
            new SecurityEvent
            {
                EventType = EventType.UnauthorizedAccessAttempt,
                Severity = EventSeverity.Critical,
                Source = "api",
                IPAddress = "1.1.1.3",
                Resource = "/admin/secrets",
                Description = "Unauthorized access",
                Timestamp = DateTimeOffset.UtcNow,
                CorrelationId = "3"
            }
        );
        await db.SaveChangesAsync();

        var service = new SecurityEventService(db, mockCurrentUser.Object, mockAudit.Object);

        var stats = await service.GetStatisticsAsync(null, null);

        stats.TotalEvents.Should().Be(3);
        stats.CriticalEvents.Should().Be(1);
        stats.HighSeverityEvents.Should().Be(1);
        stats.MediumSeverityEvents.Should().Be(1);
        stats.FailedLogins.Should().Be(2);
        stats.UnauthorizedAttempts.Should().Be(1);
    }
}
