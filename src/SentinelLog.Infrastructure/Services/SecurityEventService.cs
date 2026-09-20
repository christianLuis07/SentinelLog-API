using System.Text.Json.Nodes;
using Microsoft.EntityFrameworkCore;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Analytics;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Application.Exceptions;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Constants;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;
using SentinelLog.Infrastructure.Data;

namespace SentinelLog.Infrastructure.Services;

public class SecurityEventService : ISecurityEventService
{
    private readonly SentinelLogDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;
    private readonly IAuditLogService _auditLogService;

    public SecurityEventService(
        SentinelLogDbContext dbContext,
        ICurrentUserService currentUser,
        IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _auditLogService = auditLogService;
    }

    public async Task<SecurityEventResponse> CreateEventAsync(
        CreateSecurityEventRequest request,
        CancellationToken ct = default)
    {
        // If ActorId is specified, verify actor existence
        if (request.ActorId.HasValue)
        {
            var actorExists = await _dbContext.Users.AnyAsync(u => u.Id == request.ActorId.Value, ct);
            if (!actorExists)
            {
                throw new ValidationException(nameof(request.ActorId), $"Actor user with ID {request.ActorId.Value} does not exist.");
            }
        }

        var correlationId = !string.IsNullOrWhiteSpace(request.CorrelationId)
            ? request.CorrelationId.Trim()
            : (!string.IsNullOrWhiteSpace(_currentUser.CorrelationId) ? _currentUser.CorrelationId : Guid.NewGuid().ToString("D"));

        var timestamp = request.Timestamp ?? DateTimeOffset.UtcNow;
        var metadataString = request.Metadata != null ? request.Metadata.ToJsonString() : null;

        var securityEvent = new SecurityEvent
        {
            EventType = request.EventType,
            Severity = request.Severity,
            Source = request.Source.Trim(),
            ActorId = request.ActorId,
            IPAddress = request.IpAddress.Trim(),
            UserAgent = request.UserAgent?.Trim(),
            Resource = request.Resource.Trim(),
            Description = request.Description.Trim(),
            Timestamp = timestamp,
            CorrelationId = correlationId,
            MetadataJson = metadataString,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.SecurityEvents.Add(securityEvent);
        await _dbContext.SaveChangesAsync(ct);

        // For High or Critical severity events, record an audit entry
        if (securityEvent.Severity is EventSeverity.High or EventSeverity.Critical)
        {
            await _auditLogService.LogActionAsync(
                action: AuditActions.EventCreated,
                resourceType: "SecurityEvent",
                resourceId: securityEvent.Id.ToString(),
                details: $"High/Critical event recorded: [{securityEvent.Severity}] {securityEvent.EventType} on '{securityEvent.Resource}'.",
                ct: ct);
        }

        string? actorUsername = null;
        if (securityEvent.ActorId.HasValue)
        {
            actorUsername = await _dbContext.Users
                .Where(u => u.Id == securityEvent.ActorId.Value)
                .Select(u => u.Username)
                .FirstOrDefaultAsync(ct);
        }

        return MapToResponse(securityEvent, actorUsername);
    }

    public async Task<SecurityEventResponse> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var securityEvent = await _dbContext.SecurityEvents
            .AsNoTracking()
            .Include(e => e.Actor)
            .FirstOrDefaultAsync(e => e.Id == id, ct);

        if (securityEvent == null)
        {
            throw new NotFoundException(nameof(SecurityEvent), id);
        }

        return MapToResponse(securityEvent, securityEvent.Actor?.Username);
    }

    public async Task<PagedResult<SecurityEventResponse>> GetEventsAsync(
        SecurityEventQueryParams query,
        CancellationToken ct = default)
    {
        var dbQuery = _dbContext.SecurityEvents
            .AsNoTracking()
            .Include(e => e.Actor)
            .AsQueryable();

        // 1. Filtering
        if (query.Severity.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.Severity == query.Severity.Value);
        }

        if (query.EventType.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.EventType == query.EventType.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.Source))
        {
            dbQuery = dbQuery.Where(e => e.Source.ToLower() == query.Source.ToLower());
        }

        if (query.ActorId.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.ActorId == query.ActorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(query.IpAddress))
        {
            dbQuery = dbQuery.Where(e => e.IPAddress == query.IpAddress.Trim());
        }

        if (query.From.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.Timestamp >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.Timestamp <= query.To.Value);
        }

        // 2. Text Search across Description, Source, Resource, IPAddress
        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var term = query.Search.Trim().ToLower();
            dbQuery = dbQuery.Where(e =>
                e.Description.ToLower().Contains(term) ||
                e.Source.ToLower().Contains(term) ||
                e.Resource.ToLower().Contains(term) ||
                e.IPAddress.Contains(term));
        }

        // 3. Whitelisted Sorting
        dbQuery = ApplySorting(dbQuery, query.SortBy, query.IsDescending);

        // 4. Count & Pagination
        var totalItems = await dbQuery.LongCountAsync(ct);

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .ToListAsync(ct);

        var responses = items.Select(e => MapToResponse(e, e.Actor?.Username)).ToList();

        return PagedResult<SecurityEventResponse>.Create(responses, totalItems, query.Page, query.PageSize);
    }

    public async Task<SecurityEventStatisticsResponse> GetStatisticsAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var dbQuery = _dbContext.SecurityEvents.AsNoTracking().AsQueryable();

        if (from.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.Timestamp >= from.Value);
        }

        if (to.HasValue)
        {
            dbQuery = dbQuery.Where(e => e.Timestamp <= to.Value);
        }

        var totalEvents = await dbQuery.LongCountAsync(ct);
        var criticalEvents = await dbQuery.LongCountAsync(e => e.Severity == EventSeverity.Critical, ct);
        var highSeverityEvents = await dbQuery.LongCountAsync(e => e.Severity == EventSeverity.High, ct);
        var mediumSeverityEvents = await dbQuery.LongCountAsync(e => e.Severity == EventSeverity.Medium, ct);
        var lowSeverityEvents = await dbQuery.LongCountAsync(e => e.Severity == EventSeverity.Low, ct);
        var infoSeverityEvents = await dbQuery.LongCountAsync(e => e.Severity == EventSeverity.Info, ct);
        var failedLogins = await dbQuery.LongCountAsync(e => e.EventType == EventType.LoginFailure, ct);
        var unauthorizedAttempts = await dbQuery.LongCountAsync(e =>
            e.EventType == EventType.UnauthorizedAccessAttempt || e.EventType == EventType.ForbiddenAccessAttempt, ct);

        return new SecurityEventStatisticsResponse(
            totalEvents,
            criticalEvents,
            highSeverityEvents,
            mediumSeverityEvents,
            lowSeverityEvents,
            infoSeverityEvents,
            failedLogins,
            unauthorizedAttempts,
            from,
            to
        );
    }

    public async Task<IReadOnlyList<DailyTimelineItemResponse>> GetTimelineAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var startDate = from ?? DateTimeOffset.UtcNow.AddDays(-14);
        var endDate = to ?? DateTimeOffset.UtcNow;

        var events = await _dbContext.SecurityEvents
            .AsNoTracking()
            .Where(e => e.Timestamp >= startDate && e.Timestamp <= endDate)
            .Select(e => new { e.Timestamp, e.Severity })
            .ToListAsync(ct);

        var timeline = events
            .GroupBy(e => e.Timestamp.UtcDateTime.ToString("yyyy-MM-dd"))
            .OrderBy(g => g.Key)
            .Select(g => new DailyTimelineItemResponse(
                Date: g.Key,
                Total: g.LongCount(),
                Low: g.LongCount(x => x.Severity == EventSeverity.Low),
                Medium: g.LongCount(x => x.Severity == EventSeverity.Medium),
                High: g.LongCount(x => x.Severity == EventSeverity.High),
                Critical: g.LongCount(x => x.Severity == EventSeverity.Critical)
            ))
            .ToList();

        return timeline;
    }

    private static IQueryable<SecurityEvent> ApplySorting(
        IQueryable<SecurityEvent> query,
        string? sortBy,
        bool isDescending)
    {
        return (sortBy?.ToLowerInvariant()) switch
        {
            "createdat" => isDescending ? query.OrderByDescending(e => e.CreatedAt) : query.OrderBy(e => e.CreatedAt),
            "severity" => isDescending ? query.OrderByDescending(e => e.Severity) : query.OrderBy(e => e.Severity),
            "eventtype" => isDescending ? query.OrderByDescending(e => e.EventType) : query.OrderBy(e => e.EventType),
            "source" => isDescending ? query.OrderByDescending(e => e.Source) : query.OrderBy(e => e.Source),
            "ipaddress" => isDescending ? query.OrderByDescending(e => e.IPAddress) : query.OrderBy(e => e.IPAddress),
            "id" => isDescending ? query.OrderByDescending(e => e.Id) : query.OrderBy(e => e.Id),
            _ => isDescending ? query.OrderByDescending(e => e.Timestamp) : query.OrderBy(e => e.Timestamp)
        };
    }

    private static SecurityEventResponse MapToResponse(SecurityEvent e, string? actorUsername)
    {
        JsonObject? metadataObj = null;
        if (!string.IsNullOrWhiteSpace(e.MetadataJson))
        {
            try
            {
                metadataObj = JsonNode.Parse(e.MetadataJson) as JsonObject;
            }
            catch
            {
                metadataObj = null;
            }
        }

        return new SecurityEventResponse(
            e.Id,
            e.EventType.ToString(),
            e.Severity.ToString(),
            e.Source,
            e.ActorId,
            actorUsername,
            e.IPAddress,
            e.UserAgent,
            e.Resource,
            e.Description,
            e.Timestamp,
            e.CorrelationId,
            metadataObj,
            e.CreatedAt
        );
    }
}
