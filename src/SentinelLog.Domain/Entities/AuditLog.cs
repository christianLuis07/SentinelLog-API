namespace SentinelLog.Domain.Entities;

/// <summary>
/// Immutable audit log entity recording security-relevant actions and administrative changes.
/// Intentionally immutable: no endpoints are provided to alter or delete audit log records.
/// </summary>
public class AuditLog
{
    public long Id { get; set; }
    public long? ActorId { get; set; }
    public User? Actor { get; set; }
    public string Action { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string? ResourceId { get; set; }
    public string? IPAddress { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset Timestamp { get; set; } = DateTimeOffset.UtcNow;
    public string? Details { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
}
