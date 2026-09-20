using FluentAssertions;
using SentinelLog.Application.DTOs.Auth;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Application.Common;
using SentinelLog.Application.Validators;
using SentinelLog.Domain.Enums;
using Xunit;

namespace SentinelLog.UnitTests;

public class ValidationTests
{
    private readonly RegisterRequestValidator _registerValidator = new();
    private readonly CreateSecurityEventRequestValidator _createEventValidator = new();
    private readonly PaginationParamsValidator _paginationValidator = new();
    private readonly SecurityEventQueryParamsValidator _queryValidator = new();

    [Theory]
    [InlineData("admin", "admin@sentinellog.local", "StrongPass123!#", true)]
    [InlineData("a", "admin@sentinellog.local", "StrongPass123!#", false)] // Username too short
    [InlineData("admin", "invalid-email", "StrongPass123!#", false)] // Invalid email
    [InlineData("admin", "admin@sentinellog.local", "simple", false)] // Password too short/weak
    [InlineData("admin", "admin@sentinellog.local", "NoSpecial123", false)] // Missing special char
    [InlineData("admin", "admin@sentinellog.local", "NOLOWER123!#", false)] // Missing lowercase
    public void RegisterRequestValidator_ShouldValidateCorrectly(
        string username, string email, string password, bool expectedValid)
    {
        var request = new RegisterRequest(username, email, password);
        var result = _registerValidator.Validate(request);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void CreateSecurityEventRequestValidator_WithValidPayload_ShouldPass()
    {
        var request = new CreateSecurityEventRequest(
            EventType: EventType.LoginFailure,
            Severity: EventSeverity.High,
            Source: "auth-gateway",
            ActorId: 1,
            IpAddress: "192.168.1.100",
            UserAgent: "Mozilla/5.0",
            Resource: "/api/v1/auth/login",
            Description: "Multiple failed authentication attempts detected.",
            Timestamp: DateTimeOffset.UtcNow.AddMinutes(-1),
            CorrelationId: Guid.NewGuid().ToString(),
            Metadata: null
        );

        var result = _createEventValidator.Validate(request);
        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("not-an-ip")]
    [InlineData("999.999.999.999")]
    [InlineData("")]
    public void CreateSecurityEventRequestValidator_WithInvalidIp_ShouldFail(string badIp)
    {
        var request = new CreateSecurityEventRequest(
            EventType: EventType.LoginFailure,
            Severity: EventSeverity.High,
            Source: "auth-gateway",
            ActorId: null,
            IpAddress: badIp,
            UserAgent: null,
            Resource: "/api/v1/auth/login",
            Description: "Invalid IP test",
            Timestamp: DateTimeOffset.UtcNow,
            CorrelationId: null,
            Metadata: null
        );

        var result = _createEventValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.IpAddress));
    }

    [Fact]
    public void CreateSecurityEventRequestValidator_WithFutureTimestamp_ShouldFail()
    {
        var request = new CreateSecurityEventRequest(
            EventType: EventType.SystemError,
            Severity: EventSeverity.Critical,
            Source: "core-system",
            ActorId: null,
            IpAddress: "10.0.0.1",
            UserAgent: null,
            Resource: "/system/status",
            Description: "Future error test",
            Timestamp: DateTimeOffset.UtcNow.AddDays(1), // Future timestamp
            CorrelationId: null,
            Metadata: null
        );

        var result = _createEventValidator.Validate(request);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(request.Timestamp));
    }

    [Theory]
    [InlineData(1, 20, true)]
    [InlineData(0, 20, false)] // Page < 1
    [InlineData(1, 101, false)] // PageSize > 100
    [InlineData(1, 0, false)] // PageSize < 1
    public void PaginationParamsValidator_ShouldValidateBoundaries(int page, int pageSize, bool expectedValid)
    {
        var pagination = new PaginationParams { Page = page, PageSize = pageSize };
        var result = _paginationValidator.Validate(pagination);

        result.IsValid.Should().Be(expectedValid);
    }

    [Fact]
    public void SecurityEventQueryParamsValidator_WithInvalidSortField_ShouldFail()
    {
        var query = new SecurityEventQueryParams
        {
            Page = 1,
            PageSize = 20,
            SortBy = "DROP TABLE users; --" // SQL injection attempt or disallowed sort field
        };

        var result = _queryValidator.Validate(query);
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(query.SortBy));
    }

    [Fact]
    public void SecurityEventQueryParamsValidator_WithInvertedDateRange_ShouldFail()
    {
        var query = new SecurityEventQueryParams
        {
            From = DateTimeOffset.UtcNow.AddDays(1),
            To = DateTimeOffset.UtcNow.AddDays(-1)
        };

        var result = _queryValidator.Validate(query);
        result.IsValid.Should().BeFalse();
    }
}
