using FluentValidation;
using SentinelLog.Application.Common;
using SentinelLog.Application.DTOs.Auth;
using SentinelLog.Application.DTOs.SecurityEvents;
using SentinelLog.Application.DTOs.Users;
using SentinelLog.Application.DTOs.AuditLogs;
using SentinelLog.Domain.Enums;

namespace SentinelLog.Application.Validators;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username is required.")
            .MinimumLength(3).WithMessage("Username must be at least 3 characters.")
            .MaximumLength(50).WithMessage("Username cannot exceed 50 characters.")
            .Matches("^[a-zA-Z0-9_-]+$").WithMessage("Username can only contain alphanumeric characters, underscores, and hyphens.");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email is required.")
            .EmailAddress().WithMessage("A valid email address is required.")
            .MaximumLength(256).WithMessage("Email cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters long.")
            .MaximumLength(100).WithMessage("Password cannot exceed 100 characters.")
            .Matches(@"[A-Z]").WithMessage("Password must contain at least one uppercase letter.")
            .Matches(@"[a-z]").WithMessage("Password must contain at least one lowercase letter.")
            .Matches(@"[0-9]").WithMessage("Password must contain at least one numeric digit.")
            .Must(p => p.Any(c => !char.IsLetterOrDigit(c))).WithMessage("Password must contain at least one special character.");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.UsernameOrEmail)
            .NotEmpty().WithMessage("Username or email is required.")
            .MaximumLength(256).WithMessage("Username or email cannot exceed 256 characters.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Password is required.");
    }
}

public class CreateSecurityEventRequestValidator : AbstractValidator<CreateSecurityEventRequest>
{
    public CreateSecurityEventRequestValidator()
    {
        RuleFor(x => x.EventType)
            .IsInEnum().WithMessage("A valid EventType is required.");

        RuleFor(x => x.Severity)
            .IsInEnum().WithMessage("A valid Severity level is required.");

        RuleFor(x => x.Source)
            .NotEmpty().WithMessage("Source is required.")
            .MaximumLength(100).WithMessage("Source cannot exceed 100 characters.");

        RuleFor(x => x.IpAddress)
            .NotEmpty().WithMessage("IP address is required.")
            .MaximumLength(45).WithMessage("IP address cannot exceed 45 characters.")
            .Must(ip => !string.IsNullOrWhiteSpace(ip) && System.Net.IPAddress.TryParse(ip.Trim(), out _))
            .WithMessage("A valid IPv4 or IPv6 address is required.");

        RuleFor(x => x.UserAgent)
            .MaximumLength(500).WithMessage("UserAgent cannot exceed 500 characters.");

        RuleFor(x => x.Resource)
            .NotEmpty().WithMessage("Resource path or identifier is required.")
            .MaximumLength(500).WithMessage("Resource cannot exceed 500 characters.");

        RuleFor(x => x.Description)
            .NotEmpty().WithMessage("Description is required.")
            .MaximumLength(2000).WithMessage("Description cannot exceed 2000 characters.");

        RuleFor(x => x.Timestamp)
            .Must(t => !t.HasValue || t.Value <= DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Event timestamp cannot be in the future.");
    }
}

public class SecurityEventQueryParamsValidator : AbstractValidator<SecurityEventQueryParams>
{
    private static readonly HashSet<string> AllowedSortFields = new(StringComparer.OrdinalIgnoreCase)
    {
        "timestamp", "createdat", "severity", "eventtype", "source", "ipaddress", "id"
    };

    public SecurityEventQueryParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x.SortBy)
            .Must(s => string.IsNullOrEmpty(s) || AllowedSortFields.Contains(s))
            .WithMessage($"SortBy field must be one of: {string.Join(", ", AllowedSortFields)}.");

        RuleFor(x => x.SortDirection)
            .Must(d => string.IsNullOrEmpty(d) || d.Equals("asc", StringComparison.OrdinalIgnoreCase) || d.Equals("desc", StringComparison.OrdinalIgnoreCase))
            .WithMessage("SortDirection must be either 'asc' or 'desc'.");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From.Value <= x.To.Value)
            .WithMessage("'From' date must be earlier than or equal to 'To' date.");
    }
}

public class AuditLogQueryParamsValidator : AbstractValidator<AuditLogQueryParams>
{
    public AuditLogQueryParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");

        RuleFor(x => x)
            .Must(x => !x.From.HasValue || !x.To.HasValue || x.From.Value <= x.To.Value)
            .WithMessage("'From' date must be earlier than or equal to 'To' date.");
    }
}

public class PaginationParamsValidator : AbstractValidator<PaginationParams>
{
    public PaginationParamsValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("Page number must be at least 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("Page size must be between 1 and 100.");
    }
}

public class UpdateUserRoleRequestValidator : AbstractValidator<UpdateUserRoleRequest>
{
    public UpdateUserRoleRequestValidator()
    {
        RuleFor(x => x.Role)
            .IsInEnum().WithMessage("A valid UserRole is required.");
    }
}
