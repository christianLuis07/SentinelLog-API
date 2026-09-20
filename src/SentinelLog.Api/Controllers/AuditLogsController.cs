using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.AuditLogs;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Constants;
using ValidationException = SentinelLog.Application.Exceptions.ValidationException;

namespace SentinelLog.Api.Controllers;

[ApiController]
[Route("api/v1/audit-logs")]
[Produces("application/json", "application/problem+json")]
public class AuditLogsController : BaseApiController
{
    private readonly IAuditLogService _auditLogService;
    private readonly IValidator<AuditLogQueryParams> _queryValidator;

    public AuditLogsController(
        IAuditLogService auditLogService,
        IValidator<AuditLogQueryParams> queryValidator)
    {
        _auditLogService = auditLogService;
        _queryValidator = queryValidator;
    }

    /// <summary>
    /// Retrieve paginated immutable audit logs. Restricted to Admin and SecurityAnalyst roles.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.RequireAuditLogAccess)]
    [ProducesResponseType(typeof(PagedResult<AuditLogResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<AuditLogResponse>>> GetAuditLogs(
        [FromQuery] AuditLogQueryParams query,
        CancellationToken ct)
    {
        var validationResult = await _queryValidator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var results = await _auditLogService.GetAuditLogsAsync(query, ct);
        return Ok(results);
    }

    /// <summary>
    /// Retrieve a single audit log entry by identifier.
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = Policies.RequireAuditLogAccess)]
    [ProducesResponseType(typeof(AuditLogResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AuditLogResponse>> GetAuditLogById(
        long id,
        CancellationToken ct)
    {
        var result = await _auditLogService.GetByIdAsync(id, ct);
        return Ok(result);
    }
}
