using System.Text.Json.Serialization;

namespace VK.API.Models;

public class RecordEventRequest
{
    public int? TouristId { get; set; }

    [JsonPropertyName("poiId")]
    public int POIId { get; set; }

    public string EventType { get; set; } = string.Empty;
    public string? LanguageCode { get; set; }
    public int? DurationSeconds { get; set; }
}
