namespace SentinelLog.Application.Exceptions;

public abstract class AppException : Exception
{
    public abstract int StatusCode { get; }
    public abstract string Title { get; }

    protected AppException(string message) : base(message) { }
}

public class NotFoundException : AppException
{
    public override int StatusCode => 404;
    public override string Title => "Resource Not Found";

    public NotFoundException(string resourceName, object key)
        : base($"{resourceName} with identifier '{key}' was not found.") { }

    public NotFoundException(string message) : base(message) { }
}

public class ConflictException : AppException
{
    public override int StatusCode => 409;
    public override string Title => "Conflict";

    public ConflictException(string message) : base(message) { }
}

public class UnauthorizedException : AppException
{
    public override int StatusCode => 401;
    public override string Title => "Unauthorized";

    public UnauthorizedException(string message = "Invalid authentication credentials.") : base(message) { }
}

public class ForbiddenException : AppException
{
    public override int StatusCode => 403;
    public override string Title => "Forbidden";

    public ForbiddenException(string message = "Access to the requested resource is denied.") : base(message) { }
}

public class ValidationException : AppException
{
    public override int StatusCode => 400;
    public override string Title => "Validation Error";
    public IDictionary<string, string[]> Errors { get; }

    public ValidationException(IDictionary<string, string[]> errors)
        : base("One or more validation errors occurred.")
    {
        Errors = errors;
    }

    public ValidationException(string propertyName, string errorMessage)
        : base("One or more validation errors occurred.")
    {
        Errors = new Dictionary<string, string[]>
        {
            [propertyName] = [errorMessage]
        };
    }
}
