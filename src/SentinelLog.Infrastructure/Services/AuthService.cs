using Microsoft.EntityFrameworkCore;
using SentinelLog.Application.DTOs.Auth;
using SentinelLog.Application.DTOs.Users;
using SentinelLog.Application.Exceptions;
using SentinelLog.Application.Interfaces;
using SentinelLog.Domain.Constants;
using SentinelLog.Domain.Entities;
using SentinelLog.Domain.Enums;
using SentinelLog.Infrastructure.Data;

namespace SentinelLog.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly SentinelLogDbContext _dbContext;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;

    public AuthService(
        SentinelLogDbContext dbContext,
        IPasswordHasher passwordHasher,
        IJwtTokenGenerator jwtTokenGenerator,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request, CancellationToken ct = default)
    {
        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();

        var usernameExists = await _dbContext.Users
            .AnyAsync(u => u.Username.ToLower() == normalizedUsername, ct);
        if (usernameExists)
        {
            throw new ConflictException($"The username '{request.Username}' is already taken.");
        }

        var emailExists = await _dbContext.Users
            .AnyAsync(u => u.Email.ToLower() == normalizedEmail, ct);
        if (emailExists)
        {
            throw new ConflictException($"The email address '{request.Email}' is already registered.");
        }

        var user = new User
        {
            Username = request.Username.Trim(),
            Email = request.Email.Trim().ToLowerInvariant(),
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.Viewer, // Default least-privilege role
            IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow
        };

        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogActionAsync(
            action: AuditActions.UserCreated,
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' self-registered with role '{user.Role}'.",
            ct: ct);

        var (token, expiresIn) = _jwtTokenGenerator.GenerateToken(user);
        var userResponse = new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt
        );

        return new AuthResponse(token, "Bearer", expiresIn, userResponse);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var loginIdentifier = request.UsernameOrEmail.Trim().ToLowerInvariant();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Username.ToLower() == loginIdentifier || u.Email.ToLower() == loginIdentifier, ct);

        if (user == null)
        {
            await _auditLogService.LogActionAsync(
                action: AuditActions.UserFailedLogin,
                resourceType: "User",
                resourceId: null,
                details: $"Failed login attempt for non-existent identifier '{request.UsernameOrEmail}'.",
                ct: ct);

            throw new UnauthorizedException("Invalid username or password.");
        }

        if (!user.IsActive)
        {
            await _auditLogService.LogActionAsync(
                action: AuditActions.UserFailedLogin,
                resourceType: "User",
                resourceId: user.Id.ToString(),
                details: $"Login blocked for deactivated account '{user.Username}'.",
                ct: ct);

            throw new UnauthorizedException("This account has been deactivated. Please contact your security administrator.");
        }

        var isPasswordValid = _passwordHasher.Verify(request.Password, user.PasswordHash);
        if (!isPasswordValid)
        {
            await _auditLogService.LogActionAsync(
                action: AuditActions.UserFailedLogin,
                resourceType: "User",
                resourceId: user.Id.ToString(),
                details: $"Failed password verification for user '{user.Username}'.",
                ct: ct);

            throw new UnauthorizedException("Invalid username or password.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        await _dbContext.SaveChangesAsync(ct);

        await _auditLogService.LogActionAsync(
            action: AuditActions.UserLogin,
            resourceType: "User",
            resourceId: user.Id.ToString(),
            details: $"User '{user.Username}' authenticated successfully.",
            ct: ct);

        var (token, expiresIn) = _jwtTokenGenerator.GenerateToken(user);
        var userResponse = new UserResponse(
            user.Id,
            user.Username,
            user.Email,
            user.Role.ToString(),
            user.IsActive,
            user.CreatedAt,
            user.UpdatedAt,
            user.LastLoginAt
        );

        return new AuthResponse(token, "Bearer", expiresIn, userResponse);
    }
}
