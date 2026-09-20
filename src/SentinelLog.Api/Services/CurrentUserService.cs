using System.Security.Claims;
using SentinelLog.Application.Interfaces;

namespace SentinelLog.Api.Services;

public class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CurrentUserService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    private HttpContext? HttpContext => _httpContextAccessor.HttpContext;

    public long? UserId
    {
        get
        {
            var idClaim = HttpContext?.User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                ?? HttpContext?.User.FindFirst("sub")?.Value;

            return long.TryParse(idClaim, out var id) ? id : null;
        }
    }

    public string? Username =>
        HttpContext?.User.FindFirst(ClaimTypes.Name)?.Value
        ?? HttpContext?.User.FindFirst("unique_name")?.Value;

    public string? Role =>
        HttpContext?.User.FindFirst(ClaimTypes.Role)?.Value
        ?? HttpContext?.User.FindFirst("role")?.Value;

    public string? IpAddress
    {
        get
        {
            if (HttpContext == null) return null;

            if (HttpContext.Request.Headers.TryGetValue("X-Forwarded-For", out var forwarded))
            {
                var ip = forwarded.ToString().Split(',').FirstOrDefault()?.Trim();
                if (!string.IsNullOrWhiteSpace(ip)) return ip;
            }

            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    public string? UserAgent =>
        HttpContext?.Request.Headers.UserAgent.ToString();

    public string CorrelationId
    {
        get
        {
            if (HttpContext?.Items.TryGetValue("CorrelationId", out var correlationObj) == true &&
                correlationObj is string correlationId && !string.IsNullOrWhiteSpace(correlationId))
            {
                return correlationId;
            }

            if (HttpContext?.Request.Headers.TryGetValue("X-Correlation-ID", out var headerId) == true &&
                !string.IsNullOrWhiteSpace(headerId))
            {
                return headerId.ToString();
            }

            return Guid.NewGuid().ToString("D");
        }
    }
}
