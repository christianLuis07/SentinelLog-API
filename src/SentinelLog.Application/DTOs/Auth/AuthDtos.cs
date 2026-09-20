using SentinelLog.Application.DTOs.Users;

namespace SentinelLog.Application.DTOs.Auth;

public record RegisterRequest(
    string Username,
    string Email,
    string Password
);

public record LoginRequest(
    string UsernameOrEmail,
    string Password
);

public record AuthResponse(
    string Token,
    string TokenType,
    int ExpiresIn,
    UserResponse User
);
