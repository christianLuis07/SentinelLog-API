using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using SentinelLog.Application.Exceptions;

namespace SentinelLog.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _env;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger,
        IHostEnvironment env)
    {
        _next = next;
        _logger = logger;
        _env = env;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"]?.ToString() ?? context.TraceIdentifier;

        int statusCode;
        string title;
        string detail;
        string type;
        IDictionary<string, string[]>? validationErrors = null;

        switch (exception)
        {
            case ValidationException valEx:
                statusCode = StatusCodes.Status400BadRequest;
                title = valEx.Title;
                detail = valEx.Message;
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.1";
                validationErrors = valEx.Errors;
                _logger.LogWarning(exception, "Validation error occurred. CorrelationId: {CorrelationId}", correlationId);
                break;

            case UnauthorizedException unauthEx:
                statusCode = StatusCodes.Status401Unauthorized;
                title = unauthEx.Title;
                detail = unauthEx.Message;
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.2";
                _logger.LogWarning("Authentication failed. Message: {Message}, CorrelationId: {CorrelationId}", detail, correlationId);
                break;

            case ForbiddenException forbidEx:
                statusCode = StatusCodes.Status403Forbidden;
                title = forbidEx.Title;
                detail = forbidEx.Message;
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.4";
                _logger.LogWarning("Access forbidden. Message: {Message}, CorrelationId: {CorrelationId}", detail, correlationId);
                break;

            case NotFoundException notFoundEx:
                statusCode = StatusCodes.Status404NotFound;
                title = notFoundEx.Title;
                detail = notFoundEx.Message;
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.5";
                _logger.LogInformation("Resource not found. Message: {Message}, CorrelationId: {CorrelationId}", detail, correlationId);
                break;

            case ConflictException conflictEx:
                statusCode = StatusCodes.Status409Conflict;
                title = conflictEx.Title;
                detail = conflictEx.Message;
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.5.10";
                _logger.LogWarning("Conflict encountered. Message: {Message}, CorrelationId: {CorrelationId}", detail, correlationId);
                break;

            default:
                statusCode = StatusCodes.Status500InternalServerError;
                title = "An internal server error occurred.";
                detail = _env.IsDevelopment()
                    ? exception.Message
                    : "An unexpected error occurred processing your request. Please reference the correlation ID.";
                type = "https://datatracker.ietf.org/doc/html/rfc9110#section-15.6.1";
                _logger.LogError(exception, "Unhandled exception occurred. CorrelationId: {CorrelationId}", correlationId);
                break;
        }

        var problemDetails = new ProblemDetails
        {
            Type = type,
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        problemDetails.Extensions["correlationId"] = correlationId;

        if (validationErrors != null)
        {
            problemDetails.Extensions["errors"] = validationErrors;
        }

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = statusCode;

        var json = JsonSerializer.Serialize(problemDetails, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        });

        await context.Response.WriteAsync(json);
    }
}
