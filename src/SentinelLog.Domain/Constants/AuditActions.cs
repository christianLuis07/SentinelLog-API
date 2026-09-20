namespace SentinelLog.Domain.Constants;

public static class AuditActions
{
    public const string UserLogin = "USER_LOGIN";
    public const string UserFailedLogin = "USER_FAILED_LOGIN";
    public const string UserCreated = "USER_CREATED";
    public const string UserStatusChanged = "USER_STATUS_CHANGED";
    public const string UserRoleChanged = "USER_ROLE_CHANGED";
    public const string EventCreated = "EVENT_CREATED";
    public const string EventDeleted = "EVENT_DELETED";
    public const string ApiKeyCreated = "API_KEY_CREATED";
    public const string ApiKeyRevoked = "API_KEY_REVOKED";
}
