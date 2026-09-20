namespace SentinelLog.Application.Common;

public record SortingParams
{
    public string? SortBy { get; init; }
    public string? SortDirection { get; init; } = "desc";

    public bool IsDescending => !string.Equals(SortDirection, "asc", StringComparison.OrdinalIgnoreCase);
}
