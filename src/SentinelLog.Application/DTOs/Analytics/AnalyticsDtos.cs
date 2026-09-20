namespace SentinelLog.Application.DTOs.Analytics;

public record SecurityEventStatisticsResponse(
    long TotalEvents,
    long CriticalEvents,
    long HighSeverityEvents,
    long MediumSeverityEvents,
    long LowSeverityEvents,
    long InfoSeverityEvents,
    long FailedLogins,
    long UnauthorizedAttempts,
    DateTimeOffset? From,
    DateTimeOffset? To
);

public record DailyTimelineItemResponse(
    string Date,
    long Total,
    long Low,
    long Medium,
    long High,
    long Critical
);
