namespace SentinelLog.Domain.Enums;

public enum EventType
{
    LoginSuccess = 1,
    LoginFailure = 2,
    Logout = 3,
    PasswordChanged = 4,
    AccountCreated = 5,
    AccountDisabled = 6,
    PermissionChanged = 7,
    RoleChanged = 8,
    ResourceCreated = 9,
    ResourceUpdated = 10,
    ResourceDeleted = 11,
    UnauthorizedAccessAttempt = 12,
    ForbiddenAccessAttempt = 13,
    RateLimitExceeded = 14,
    SuspiciousActivity = 15,
    ApiKeyCreated = 16,
    ApiKeyRevoked = 17,
    SystemError = 18
}
