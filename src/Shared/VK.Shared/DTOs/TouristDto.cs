namespace VK.Shared.DTOs;

using System.Text.Json.Serialization;

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

    [JsonPropertyName("pointOfInterestId")]
    public int PointOfInterestId { get; set; }

    public string? TriggerMethod { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    public string? LanguageCode { get; set; }

    [JsonIgnore]
    public int EffectivePoiId => PoiId > 0 ? PoiId : PointOfInterestId;
}

public class VisitHistoryDto
{
    public int VisitId { get; set; }
    public int PoiId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public string? PoiImageUrl { get; set; }
    public DateTime VisitedAt { get; set; }
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
