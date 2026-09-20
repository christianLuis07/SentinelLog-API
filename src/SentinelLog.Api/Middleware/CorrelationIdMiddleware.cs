using Serilog.Context;

namespace SentinelLog.Api.Middleware;

public class CorrelationIdMiddleware
{
    private const string CorrelationIdHeader = "X-Correlation-ID";
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate _next)
    {
        this._next = _next;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        string correlationId;

        if (context.Request.Headers.TryGetValue(CorrelationIdHeader, out var values) &&
            !string.IsNullOrWhiteSpace(values.FirstOrDefault()))
        {
            var candidate = values.First()!.Trim();
            // Basic sanitization: check length and permitted characters
            if (candidate.Length <= 100 && candidate.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_'))
            {
                correlationId = candidate;
            }
            else
            {
                correlationId = Guid.NewGuid().ToString("D");
            }
        }
        else
        {
            correlationId = Guid.NewGuid().ToString("D");
        }

        context.Items["CorrelationId"] = correlationId;

        // Ensure header is returned in response
        context.Response.OnStarting(() =>
        {
            if (!context.Response.Headers.ContainsKey(CorrelationIdHeader))
            {
                context.Response.Headers.Append(CorrelationIdHeader, correlationId);
            }
            return Task.CompletedTask;
        });

        using (LogContext.PushProperty("CorrelationId", correlationId))
        {
            await _next(context);
        }
    }
}
