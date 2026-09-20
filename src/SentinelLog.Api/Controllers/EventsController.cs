using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Analytics;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Constants;
using ValidationException = SentinelLog.Application.Exceptions.ValidationException;

namespace SentinelLog.Api.Controllers;

[ApiController]
[Route("api/v1/events")]
[Produces("application/json", "application/problem+json")]
public class EventsController : BaseApiController
{
    private readonly ISecurityEventService _eventService;
    private readonly IValidator<CreateSecurityEventRequest> _createValidator;
    private readonly IValidator<SecurityEventQueryParams> _queryValidator;

    public EventsController(
        ISecurityEventService eventService,
        IValidator<CreateSecurityEventRequest> createValidator,
        IValidator<SecurityEventQueryParams> queryValidator)
    {
        _eventService = eventService;
        _createValidator = createValidator;
        _queryValidator = queryValidator;
    }

    /// <summary>
    /// Ingest a new security event into the system.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Policies.RequireEventIngestion)]
    [EnableRateLimiting("events-ingest")]
    [ProducesResponseType(typeof(SecurityEventResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<SecurityEventResponse>> IngestEvent(
        [FromBody] CreateSecurityEventRequest request,
        CancellationToken ct)
    {
        var validationResult = await _createValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var response = await _eventService.CreateEventAsync(request, ct);
        return CreatedAtAction(nameof(GetEventById), new { id = response.Id }, response);
    }

    /// <summary>
    /// Retrieve paginated security events with multi-criteria filtering, text search, and safe sorting.
    /// </summary>
    [HttpGet]
    [Authorize(Policy = Policies.RequireEventRead)]
    [ProducesResponseType(typeof(PagedResult<SecurityEventResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<SecurityEventResponse>>> GetEvents(
        [FromQuery] SecurityEventQueryParams query,
        CancellationToken ct)
    {
        var validationResult = await _queryValidator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var results = await _eventService.GetEventsAsync(query, ct);
        return Ok(results);
    }

    /// <summary>
    /// Get aggregated security event statistics, counts, and alert volume.
    /// </summary>
    [HttpGet("statistics")]
    [Authorize(Policy = Policies.RequireEventStatistics)]
    [ProducesResponseType(typeof(SecurityEventStatisticsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<SecurityEventStatisticsResponse>> GetStatistics(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ValidationException(nameof(from), "'from' date cannot be later than 'to' date.");
        }

        var stats = await _eventService.GetStatisticsAsync(from, to, ct);
        return Ok(stats);
    }

    /// <summary>
    /// Get daily aggregated security event timelines.
    /// </summary>
    [HttpGet("timeline")]
    [Authorize(Policy = Policies.RequireEventStatistics)]
    [ProducesResponseType(typeof(IReadOnlyList<DailyTimelineItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IReadOnlyList<DailyTimelineItemResponse>>> GetTimeline(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken ct)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ValidationException(nameof(from), "'from' date cannot be later than 'to' date.");
        }

        var timeline = await _eventService.GetTimelineAsync(from, to, ct);
        return Ok(timeline);
    }

    /// <summary>
    /// Retrieve single security event details by identifier.
    /// </summary>
    [HttpGet("{id:long}")]
    [Authorize(Policy = Policies.RequireEventRead)]
    [ProducesResponseType(typeof(SecurityEventResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SecurityEventResponse>> GetEventById(
        long id,
        CancellationToken ct)
    {
        var response = await _eventService.GetByIdAsync(id, ct);
        return Ok(response);
    }
}
