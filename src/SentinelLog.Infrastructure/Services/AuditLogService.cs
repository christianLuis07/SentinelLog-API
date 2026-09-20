using Microsoft.EntityFrameworkCore;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.AuditLogs;
using SentinelLog.Application.Exceptions;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Entities;
using SentinelLog.Infrastructure.Data;

namespace SentinelLog.Infrastructure.Services;

public class AuditLogService : IAuditLogService
{
    private readonly SentinelLogDbContext _dbContext;
    private readonly ICurrentUserService _currentUser;

    public AuditLogService(SentinelLogDbContext dbContext, ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
    }

    public async Task LogActionAsync(
        string action,
        string resourceType,
        string? resourceId,
        string? details,
        CancellationToken ct = default)
    {
        var auditLog = new AuditLog
        {
            ActorId = _currentUser.UserId,
            Action = action,
            ResourceType = resourceType,
            ResourceId = resourceId,
            IPAddress = _currentUser.IpAddress,
            UserAgent = _currentUser.UserAgent,
            Timestamp = DateTimeOffset.UtcNow,
            Details = details,
            CorrelationId = _currentUser.CorrelationId
        };

        _dbContext.AuditLogs.Add(auditLog);
        await _dbContext.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<AuditLogResponse>> GetAuditLogsAsync(
        AuditLogQueryParams query,
        CancellationToken ct = default)
    {
        var dbQuery = _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.Actor)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            dbQuery = dbQuery.Where(a => a.Action.ToUpper() == query.Action.ToUpper());
        }

        if (!string.IsNullOrWhiteSpace(query.ResourceType))
        {
            dbQuery = dbQuery.Where(a => a.ResourceType.ToLower() == query.ResourceType.ToLower());
        }

        if (query.ActorId.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.ActorId == query.ActorId.Value);
        }

        if (query.From.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Timestamp >= query.From.Value);
        }

        if (query.To.HasValue)
        {
            dbQuery = dbQuery.Where(a => a.Timestamp <= query.To.Value);
        }

        dbQuery = query.IsDescending
            ? dbQuery.OrderByDescending(a => a.Timestamp)
            : dbQuery.OrderBy(a => a.Timestamp);

        var totalItems = await dbQuery.LongCountAsync(ct);

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(a => new AuditLogResponse(
                a.Id,
                a.ActorId,
                a.Actor != null ? a.Actor.Username : null,
                a.Action,
                a.ResourceType,
                a.ResourceId,
                a.IPAddress,
                a.UserAgent,
                a.Timestamp,
                a.Details,
                a.CorrelationId
            ))
            .ToListAsync(ct);

        return PagedResult<AuditLogResponse>.Create(items, totalItems, query.Page, query.PageSize);
    }

    public async Task<AuditLogResponse> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var log = await _dbContext.AuditLogs
            .AsNoTracking()
            .Include(a => a.Actor)
            .FirstOrDefaultAsync(a => a.Id == id, ct);

        if (log == null)
        {
            throw new NotFoundException(nameof(AuditLog), id);
        }

        return new AuditLogResponse(
            log.Id,
            log.ActorId,
            log.Actor?.Username,
            log.Action,
            log.ResourceType,
            log.ResourceId,
            log.IPAddress,
            log.UserAgent,
            log.Timestamp,
            log.Details,
            log.CorrelationId
        );
    }
}
