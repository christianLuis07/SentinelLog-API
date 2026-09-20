using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Users;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Constants;
using ValidationException = SentinelLog.Application.Exceptions.ValidationException;

namespace SentinelLog.Api.Controllers;

[ApiController]
[Route("api/v1/users")]
[Authorize(Policy = Policies.RequireAdmin)]
[Produces("application/json", "application/problem+json")]
public class UsersController : BaseApiController
{
    private readonly IUserService _userService;
    private readonly IValidator<PaginationParams> _paginationValidator;
    private readonly IValidator<UpdateUserRoleRequest> _roleValidator;

    public UsersController(
        IUserService userService,
        IValidator<PaginationParams> paginationValidator,
        IValidator<UpdateUserRoleRequest> roleValidator)
    {
        _userService = userService;
        _paginationValidator = paginationValidator;
        _roleValidator = roleValidator;
    }

    /// <summary>
    /// Retrieve paginated list of registered users. Restricted to Admin.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<UserResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<PagedResult<UserResponse>>> GetUsers(
        [FromQuery] PaginationParams query,
        CancellationToken ct)
    {
        var validationResult = await _paginationValidator.ValidateAsync(query, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var results = await _userService.GetUsersAsync(query, ct);
        return Ok(results);
    }

    /// <summary>
    /// Retrieve user details by identifier. Restricted to Admin.
    /// </summary>
    [HttpGet("{id:long}")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> GetUserById(
        long id,
        CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        return Ok(user);
    }

    /// <summary>
    /// Update user activation status (Active / Inactive). Restricted to Admin.
    /// </summary>
    [HttpPatch("{id:long}/status")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateStatus(
        long id,
        [FromBody] UpdateUserStatusRequest request,
        CancellationToken ct)
    {
        var user = await _userService.UpdateStatusAsync(id, request, ct);
        return Ok(user);
    }

    /// <summary>
    /// Update user security role. Restricted to Admin.
    /// </summary>
    [HttpPatch("{id:long}/role")]
    [ProducesResponseType(typeof(UserResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserResponse>> UpdateRole(
        long id,
        [FromBody] UpdateUserRoleRequest request,
        CancellationToken ct)
    {
        var validationResult = await _roleValidator.ValidateAsync(request, ct);
        if (!validationResult.IsValid)
        {
            var errors = validationResult.ToDictionary();
            throw new ValidationException(errors);
        }

        var user = await _userService.UpdateRoleAsync(id, request, ct);
        return Ok(user);
    }
}
