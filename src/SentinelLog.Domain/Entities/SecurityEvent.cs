using SentinelLog.Domain.Enums;

namespace SentinelLog.Domain.Entities;

public class SecurityEvent
{
    public long Id { get; set; }
    public EventType EventType { get; set; }
    public EventSeverity Severity { get; set; }
    public string Source { get; set; } = string.Empty;
    public long? ActorId { get; set; }
    public User? Actor { get; set; }
    public string IPAddress { get; set; } = string.Empty;
    public string? UserAgent { get; set; }
    public string Resource { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset Timestamp { get; set; }
    public string CorrelationId { get; set; } = string.Empty;
    
    /// <summary>
    /// Stored as JSONB in PostgreSQL.
    /// </summary>
    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}
