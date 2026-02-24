namespace VK.Shared.DTOs;

public class RegisterTouristRequest
{
    public string DeviceId { get; set; } = string.Empty;
    public string? PreferredLanguage { get; set; }
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }
}

public class UpdateLocationRequest
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

public class LogVisitRequest
{
    public int PoiId { get; set; }
}

public class VisitHistoryDto
{
    public int VisitId { get; set; }
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string? PoiImageUrl { get; set; }
    public DateTime VisitedAt { get; set; }
    public int DurationMinutes { get; set; }
}

public class AddFavoriteRequest
{
    public int PoiId { get; set; }
    public string? Note { get; set; }
}

public class SubmitRatingRequest
{
    public int PoiId { get; set; }
    public int Score { get; set; } // 1-5
    public string? Comment { get; set; }
    public string? LanguageCode { get; set; }
}
