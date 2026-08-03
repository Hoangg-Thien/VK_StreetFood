namespace VK.Shared.DTOs;

public class RecordEventResultDto
{
    public bool Success { get; set; } = true;
    public int EventId { get; set; }
}

public class POISummaryDto
{
    public int TotalViews { get; set; }
    public int TotalScans { get; set; }
    public int TotalAudioPlays { get; set; }
    public int TotalAudioCompletes { get; set; }
    public int UniqueVisitors { get; set; }
    public double AverageDuration { get; set; }
    public List<LanguageBreakdownDto> LanguageBreakdown { get; set; } = new();
    public List<EventsByDayDto> EventsByDay { get; set; } = new();
}

public class LanguageBreakdownDto
{
    public string Language { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class EventsByDayDto
{
    public DateTime Date { get; set; }
    public int Count { get; set; }
}

public class DashboardOverviewDto
{
    public int TotalEvents { get; set; }
    public int TotalVisits { get; set; }
    public int TotalRatings { get; set; }
    public int UniqueVisitors { get; set; }
    public double AverageRating { get; set; }
}

public class DashboardTopPoiDto
{
    public int PoiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public int TotalEvents { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalRatings { get; set; }
}

public class EventsByTypeDto
{
    public string EventType { get; set; } = string.Empty;
    public int Count { get; set; }
}

public class DailyTrendDto
{
    public DateTime Date { get; set; }
    public int Events { get; set; }
    public int UniqueVisitors { get; set; }
}

public class DashboardDto
{
    public DashboardOverviewDto Overview { get; set; } = new();
    public List<DashboardTopPoiDto> TopPOIs { get; set; } = new();
    public List<EventsByTypeDto> EventsByType { get; set; } = new();
    public List<LanguageBreakdownDto> VisitorsByLanguage { get; set; } = new();
    public List<DailyTrendDto> DailyTrend { get; set; } = new();
}

public class TopPoiDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int VisitCount { get; set; }
    public int AudioPlayCount { get; set; }
    public double AverageRating { get; set; }
    public double AverageListenMinutes { get; set; }
}

public class TopListenedPoiDto
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public int AudioPlayCount { get; set; }
    public int AudioCompleteCount { get; set; }
    public int UniqueListeners { get; set; }
    public double CompletionRate { get; set; }
}

public class AvgListenPoiDto
{
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public double AverageDurationSeconds { get; set; }
    public int SampleCount { get; set; }
}

public class HeatmapPointDto
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Weight { get; set; }
}

public class AnonymousRoutePointDto
{
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
    public DateTime? VisitedAt { get; set; }
}

public class AnonymousRouteDto
{
    public string AnonymousVisitorId { get; set; } = string.Empty;
    public int PointCount { get; set; }
    public DateTime? FirstSeenAt { get; set; }
    public DateTime? LastSeenAt { get; set; }
    public List<AnonymousRoutePointDto> Points { get; set; } = new();
}
