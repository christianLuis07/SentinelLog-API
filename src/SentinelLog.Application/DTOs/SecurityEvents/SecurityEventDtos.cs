using System.Text.Json.Nodes;
using SentinelLog.Application.Common;
using SentinelLog.Domain.Enums;

namespace SentinelLog.Application.DTOs.SecurityEvents;

public record CreateSecurityEventRequest(
    EventType EventType,
    EventSeverity Severity,
    string Source,
    long? ActorId,
    string IpAddress,
    string? UserAgent,
    string Resource,
    string Description,
    DateTimeOffset? Timestamp,
    string? CorrelationId,
    JsonObject? Metadata
);

public record SecurityEventResponse(
    long Id,
    string EventType,
    string Severity,
    string Source,
    long? ActorId,
    string? ActorUsername,
    string IpAddress,
    string? UserAgent,
    string Resource,
    string Description,
    DateTimeOffset Timestamp,
    string CorrelationId,
    JsonObject? Metadata,
    DateTimeOffset CreatedAt
);

public record SecurityEventQueryParams : PaginationParams
{
    public EventSeverity? Severity { get; init; }
    public EventType? EventType { get; init; }
    public string? Source { get; init; }
    public long? ActorId { get; init; }
    public string? IpAddress { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? Search { get; init; }
    public string? SortBy { get; init; } = "timestamp";
    public string? SortDirection { get; init; } = "desc";

    public bool IsDescending => !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
}
