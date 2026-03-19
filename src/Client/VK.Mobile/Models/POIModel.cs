using System.Text.Json.Serialization;

namespace VK.Mobile.Models;

public class POIModel
{
    /// <summary>API trả về "poiId" cho list, "poiId" cho detail</summary>
    [JsonPropertyName("poiId")]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;

    [JsonPropertyName("qrCode")]
    public string? QrCode { get; set; }

    public string? ImageUrl { get; set; }

    /// <summary>API trả về "category" (string)</summary>
    [JsonPropertyName("category")]
    public string? CategoryName { get; set; }

    public int ViewCount { get; set; }
    public double? AverageRating { get; set; }
    public double? DistanceKm { get; set; }
    public int TotalRatings { get; set; }
    public List<string> Tags { get; set; } = new();

    /// <summary>
    /// Mức ưu tiên thuyết minh khi nhiều POI cùng vào geofence.
    /// Cao hơn = ưu tiên hơn. Mặc định 0.
    /// </summary>
    [JsonPropertyName("priority")]
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Bán kính kích hoạt riêng cho POI (mét). Nếu null thì dùng geofence radius mặc định trong Settings.
    /// </summary>
    [JsonPropertyName("triggerRadiusMeters")]
    public double? TriggerRadiusMeters { get; set; }

    // Audio info
    public AudioInfo? Audio { get; set; }
}

public class AudioInfo
{
    [JsonPropertyName("audioId")]
    public int Id { get; set; }

    public string LanguageCode { get; set; } = "vi";
    public string? AudioFileUrl { get; set; }

    [JsonPropertyName("durationInSeconds")]
    public int? DurationSeconds { get; set; }

    public string? TextContent { get; set; }
}

/// <summary>Kết quả từ GET /api/audio/poi/{id}?languageCode=</summary>
public class AudioContentResult
{
    [JsonPropertyName("audioId")]
    public int AudioId { get; set; }

    [JsonPropertyName("poiId")]
    public int PoiId { get; set; }

    [JsonPropertyName("languageCode")]
    public string LanguageCode { get; set; } = "vi";

    [JsonPropertyName("audioFileUrl")]
    public string? AudioFileUrl { get; set; }

    [JsonPropertyName("textContent")]
    public string? TextContent { get; set; }

    [JsonPropertyName("durationInSeconds")]
    public int? DurationInSeconds { get; set; }

    [JsonPropertyName("isGenerated")]
    public bool IsGenerated { get; set; }

    /// <summary>True khi server trả về ngôn ngữ fallback thay vì ngôn ngữ yêu cầu.</summary>
    [JsonPropertyName("isFallback")]
    public bool IsFallback { get; set; }
}

public class POIDetailModel : POIModel
{
    public string? CategoryDescription { get; set; }
    public List<AudioInfo> AudioContents { get; set; } = new();
    public List<VendorInfo> Vendors { get; set; } = new();

    [JsonPropertyName("recentRatings")]
    public List<RatingInfo> Ratings { get; set; } = new();
}

public class VendorInfo
{
    [JsonPropertyName("vendorId")]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal AverageRating { get; set; }
    public string? ImageUrl { get; set; }
    public List<ProductInfo> Products { get; set; } = new();
    public List<OpeningHoursInfo> OpeningHours { get; set; } = new();
}

public class ProductInfo
{
    [JsonPropertyName("productId")]
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}

public class OpeningHoursInfo
{
    public int DayOfWeek { get; set; }
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}

public class RatingInfo
{
    [JsonPropertyName("score")]
    public int RatingValue { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Một điểm vị trí ẩn danh dùng cho heatmap.</summary>
public class HeatmapPoint
{
    public HeatmapPoint() { }
    public HeatmapPoint(double latitude, double longitude)
    {
        Latitude = latitude;
        Longitude = longitude;
        Timestamp = DateTime.UtcNow;
    }

    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime Timestamp { get; set; }
}

public class TopPOIModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? CategoryName { get; set; }
    public int VisitCount { get; set; }
    public int AudioPlayCount { get; set; }
    public double AverageRating { get; set; }
    public double AverageListenMinutes { get; set; }
}
