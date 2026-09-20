using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Auth;
using SentinelLog.Application.DTOs.Users;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Application.DTOs.Analytics;
using SentinelLog.Application.DTOs.AuditLogs;

namespace SentinelLog.Application.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);
}

public interface ISecurityEventService
{
    Task<SecurityEventResponse> CreateEventAsync(CreateSecurityEventRequest request, CancellationToken ct = default);
    Task<SecurityEventResponse> GetByIdAsync(long id, CancellationToken ct = default);
    Task<PagedResult<SecurityEventResponse>> GetEventsAsync(SecurityEventQueryParams query, CancellationToken ct = default);
    Task<SecurityEventStatisticsResponse> GetStatisticsAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
    Task<IReadOnlyList<DailyTimelineItemResponse>> GetTimelineAsync(DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default);
}

public interface IAuditLogService
{
    Task LogActionAsync(string action, string resourceType, string? resourceId, string? details, CancellationToken ct = default);
    Task<PagedResult<AuditLogResponse>> GetAuditLogsAsync(AuditLogQueryParams query, CancellationToken ct = default);
    Task<AuditLogResponse> GetByIdAsync(long id, CancellationToken ct = default);
}

public interface IUserService
{
    Task<PagedResult<UserResponse>> GetUsersAsync(PaginationParams query, CancellationToken ct = default);
    Task<UserResponse> GetByIdAsync(long id, CancellationToken ct = default);
    Task<UserResponse> UpdateStatusAsync(long id, UpdateUserStatusRequest request, CancellationToken ct = default);
    Task<UserResponse> UpdateRoleAsync(long id, UpdateUserRoleRequest request, CancellationToken ct = default);
}
