namespace VK.Shared.DTOs;

public class POIListItemDto
{
    public int POIId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public decimal AverageRating { get; set; }
    public int TotalRatings { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public double? DistanceKm { get; set; }

    /// <summary>
    /// Geofence trigger priority: larger value wins when multiple POIs are in range.
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Optional per-POI trigger radius (meters). If null, clients use app-level default radius.
    /// </summary>
    public double? TriggerRadiusMeters { get; set; }
}

public class POIDetailDto : POIListItemDto
{
    public AudioContentDto? Audio { get; set; }
    public List<VendorDetailDto> Vendors { get; set; } = new();
    public List<RatingDto> RecentRatings { get; set; } = new();
}

public class VendorDetailDto : VendorDto
{
    public string? Email { get; set; }
    public int TotalReviews { get; set; }
    public string? ImageUrl { get; set; }
    public List<OpeningHoursDto> OpeningHours { get; set; } = new();
}

public class OpeningHoursDto
{
    public int DayOfWeek { get; set; }
    public string OpenTime { get; set; } = string.Empty;
    public string CloseTime { get; set; } = string.Empty;
    public bool IsClosed { get; set; }
}

public class RatingDto
{
    public int Score { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class CategoryDto
{
    public int CategoryId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? IconUrl { get; set; }
}
