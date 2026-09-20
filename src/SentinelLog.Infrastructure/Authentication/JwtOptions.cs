namespace SentinelLog.Infrastructure.Authentication;

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public string Issuer { get; set; } = "SentinelLog";
    public string Audience { get; set; } = "SentinelLog.Client";
    public int ExpirationMinutes { get; set; } = 60;
}
