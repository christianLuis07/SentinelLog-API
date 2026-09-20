using SentinelLog.Domain.Enums;

namespace SentinelLog.Domain.Entities;

public class User
{
    public long Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public UserRole Role { get; set; } = UserRole.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt { get; set; }
    public DateTimeOffset? LastLoginAt { get; set; }

    public ICollection<SecurityEvent> SecurityEvents { get; set; } = new List<SecurityEvent>();
    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();
}
