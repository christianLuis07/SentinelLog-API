using SentinelLog.Domain.Enums;

namespace SentinelLog.Application.DTOs.Users;

public record UserResponse(
    long Id,
    string Username,
    string Email,
    string Role,
    bool IsActive,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt,
    DateTimeOffset? LastLoginAt
);

public record UpdateUserStatusRequest(
    bool IsActive
);

public record UpdateUserRoleRequest(
    UserRole Role
);
