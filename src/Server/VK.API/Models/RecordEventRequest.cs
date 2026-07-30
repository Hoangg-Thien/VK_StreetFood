using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace VK.API.Models;

public class RecordEventRequest
{
    public int? TouristId { get; set; }

    [Range(1, int.MaxValue)]
    [JsonPropertyName("poiId")]
    public int POIId { get; set; }

    [Required]
    [MaxLength(64)]
    public string EventType { get; set; } = string.Empty;

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    [Range(0, 86400)]
    public int? DurationSeconds { get; set; }
}
