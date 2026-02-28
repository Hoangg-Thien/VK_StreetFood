using System.Text.Json.Serialization;

namespace VK.Mobile.Models;

public class TouristModel
{
    [JsonPropertyName("touristId")]
    public int Id { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "vi";
    public int TotalVisits { get; set; }
    public double? LastLatitude { get; set; }
    public double? LastLongitude { get; set; }
}

public class VisitLogModel
{
    public int Id { get; set; }
    public int PointOfInterestId { get; set; }
    public string PoiName { get; set; } = string.Empty;
    public DateTime VisitedAt { get; set; }
    public string? TriggerMethod { get; set; } // "qr_scan" or "geofence"
}

public class FavoriteModel
{
    public int Id { get; set; }
    public int PointOfInterestId { get; set; }
    public POIModel? Poi { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class TouristStatsModel
{
    public int TotalVisits { get; set; }
    public int TotalAudioPlays { get; set; }
    public int TotalQRScans { get; set; }
    public int TotalGeofenceEnters { get; set; }
    public double TotalAudioMinutes { get; set; }
    public string? MostVisitedPOI { get; set; }
    public string? FavoriteLanguage { get; set; }
}
