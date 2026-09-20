namespace SentinelLog.Domain.Constants;

public static class Roles
{
    public const string Admin = "Admin";
    public const string SecurityAnalyst = "SecurityAnalyst";
    public const string Service = "Service";
    public const string Viewer = "Viewer";

    public static readonly IReadOnlyList<string> All = [Admin, SecurityAnalyst, Service, Viewer];
}
