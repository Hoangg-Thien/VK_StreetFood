using System.Text.Json.Serialization;

namespace VK.Mobile.Models;

public class TourModel
{
    [JsonPropertyName("tourId")]
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Emoji { get; set; } = "🍜";

    public int? EstimatedDurationMinutes { get; set; }

    public string Status { get; set; } = "draft";

    public int StopsCount { get; set; }

    public int? FirstPOIId { get; set; }

    public string? CoverImageUrl { get; set; }

    public List<TourPointModel> Points { get; set; } = new();
}

public class TourPointModel
{
    [JsonPropertyName("poiId")]
    public int POIId { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Address { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public double Latitude { get; set; }

    public double Longitude { get; set; }

    public int SortOrder { get; set; }
}