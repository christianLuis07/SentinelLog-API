using SentinelLog.Application.Common;

namespace SentinelLog.Application.DTOs.AuditLogs;

public record AuditLogResponse(
    long Id,
    long? ActorId,
    string? ActorUsername,
    string Action,
    string ResourceType,
    string? ResourceId,
    string? IpAddress,
    string? UserAgent,
    DateTimeOffset Timestamp,
    string? Details,
    string CorrelationId
);

public record AuditLogQueryParams : PaginationParams
{
    public string? Action { get; init; }
    public string? ResourceType { get; init; }
    public long? ActorId { get; init; }
    public DateTimeOffset? From { get; init; }
    public DateTimeOffset? To { get; init; }
    public string? SortDirection { get; init; } = "desc";

    public bool IsDescending => !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
}
