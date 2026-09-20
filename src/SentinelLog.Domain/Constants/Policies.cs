namespace SentinelLog.Domain.Constants;

public static class Policies
{
    public const string RequireAdmin = "RequireAdmin";
    public const string RequireSecurityAnalystOrAdmin = "RequireSecurityAnalystOrAdmin";
    public const string RequireEventIngestion = "RequireEventIngestion";
    public const string RequireEventRead = "RequireEventRead";
    public const string RequireEventStatistics = "RequireEventStatistics";
    public const string RequireAuditLogAccess = "RequireAuditLogAccess";
    public const string RequireUserManagement = "RequireUserManagement";
}
