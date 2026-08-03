namespace VK.Shared.DTOs;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public class RegisterTouristRequest
{
    [Required]
    [MaxLength(256)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? PreferredLanguage { get; set; }

    [Range(-90.0, 90.0)]
    public double? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double? Longitude { get; set; }
}

public class UpdateLocationRequest
{
    [Range(-90.0, 90.0)]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double Longitude { get; set; }
}

public class LogVisitRequest
{
    public int POIId { get; set; }

    [JsonPropertyName("pointOfInterestId")]
    public int PointOfInterestId { get; set; }

    [MaxLength(64)]
    public string? TriggerMethod { get; set; }

    [Range(-90.0, 90.0)]
    public double? Latitude { get; set; }

    [Range(-180.0, 180.0)]
    public double? Longitude { get; set; }

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    [JsonIgnore]
    public int EffectivePOIId => POIId > 0 ? POIId : PointOfInterestId;
}

public class VisitHistoryDto
{
    public int VisitId { get; set; }
    public int POIId { get; set; }
    public string POIName { get; set; } = string.Empty;
    public string? POIImageUrl { get; set; }
    public DateTime VisitedAt { get; set; }
}

public class AddFavoriteRequest
{
    [Range(1, int.MaxValue)]
    public int POIId { get; set; }

    [MaxLength(500)]
    public string? Note { get; set; }
}

public class SubmitRatingRequest
{
    [Range(1, int.MaxValue)]
    public int POIId { get; set; }

    /// <summary>Star rating: must be between 1 and 5 (inclusive).</summary>
    [Range(1, 5)]
    public int Score { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    [MaxLength(10)]
    public string? LanguageCode { get; set; }
}

public class UpdateLocationResultDto
{
    public bool Success { get; set; } = true;
    public List<NearbyPoiCheckDto> NearbyPOIs { get; set; } = new();
}

public class NearbyPoiCheckDto
{
    public int PoiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public double DistanceMeters { get; set; }
    public bool ShouldTriggerAudio { get; set; }
}

public class TouristStatsDto
{
    public int TotalVisits { get; set; }
    public int TotalAudioPlays { get; set; }
    public int TotalQRScans { get; set; }
    public int TotalGeofenceEnters { get; set; }
    public double TotalAudioMinutes { get; set; }
    public string? MostVisitedPOI { get; set; }
    public string? FavoriteLanguage { get; set; }
}

