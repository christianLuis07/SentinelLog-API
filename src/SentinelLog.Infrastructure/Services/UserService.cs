using Microsoft.EntityFrameworkCore;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Users;
using SentinelLog.Application.Exceptions;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Constants;
using SentinelLog.Domain.Entities;
using SentinelLog.Infrastructure.Data;

namespace SentinelLog.Infrastructure.Services;

public class UserService : IUserService
{
    private readonly SentinelLogDbContext _dbContext;
    private readonly IAuditLogService _auditLogService;

    public UserService(SentinelLogDbContext dbContext, IAuditLogService auditLogService)
    {
        _dbContext = dbContext;
        _auditLogService = auditLogService;
    }

    public async Task<PagedResult<UserResponse>> GetUsersAsync(
        PaginationParams query,
        CancellationToken ct = default)
    {
        var dbQuery = _dbContext.Users.AsNoTracking().OrderBy(u => u.Id);

        var totalItems = await dbQuery.LongCountAsync(ct);

        var items = await dbQuery
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(u => new UserResponse(
                u.Id,
                u.Username,
                u.Email,
                u.Role.ToString(),
                u.IsActive,
                u.CreatedAt,
                u.UpdatedAt,
                u.LastLoginAt
            ))
            .ToListAsync(ct);

        return PagedResult<UserResponse>.Create(items, totalItems, query.Page, query.PageSize);
    }

    public async Task<UserResponse> GetByIdAsync(long id, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user == null)
        {
            throw new NotFoundException(nameof(User), id);
        }

        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt
        );
    }

    public async Task<UserResponse> UpdateStatusAsync(
        long id,
        UpdateUserStatusRequest request,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), id);
        }

        var oldStatus = user.IsActive;
        user.IsActive = request.IsActive;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogActionAsync(
            action: AuditActions.UserStatusChanged,
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' status changed from {(oldStatus ? "Active" : "Inactive")} to {(user.IsActive ? "Active" : "Inactive")}.",
            ct: ct);

        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt
        );
    }

    public async Task<UserResponse> UpdateRoleAsync(
        long id,
        UpdateUserRoleRequest request,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == id, ct);
        if (user == null)
        {
            throw new NotFoundException(nameof(User), id);
        }

        var oldRole = user.Role;
        user.Role = request.Role;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogActionAsync(
            action: AuditActions.UserRoleChanged,
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' role changed from '{oldRole}' to '{user.Role}'.",
            ct: ct);

        return new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt
        );
    }
}
