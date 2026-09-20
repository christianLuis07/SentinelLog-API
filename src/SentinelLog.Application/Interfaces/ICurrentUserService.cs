namespace SentinelLog.Application.Interfaces;

public interface ICurrentUserService
{
    long? UserId { get; }
    string? Username { get; }
    string? Role { get; }
    string? IpAddress { get; }
    string? UserAgent { get; }
    string CorrelationId { get; }
}
