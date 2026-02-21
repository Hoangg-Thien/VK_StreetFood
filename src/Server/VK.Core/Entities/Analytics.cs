using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class Analytics : BaseEntity
{
    [Required]
    public Guid PointOfInterestId { get; set; }

    public Guid? TouristId { get; set; }

    [Required]
    [MaxLength(50)]
    public string EventType { get; set; } = string.Empty; // view, qr_scan, audio_play, audio_complete

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    public double? Latitude { get; set; }

    public double? Longitude { get; set; }

    [MaxLength(200)]
    public string? DeviceInfo { get; set; }

    [MaxLength(100)]
    public string? UserAgent { get; set; }

    public int? DurationSeconds { get; set; }

    public DateTime EventTimestamp { get; set; } = DateTime.UtcNow;

    // Navigation properties
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
    public virtual Tourist? Tourist { get; set; }
}

public class Rating : BaseEntity
{
    [Required]
    public Guid PointOfInterestId { get; set; }

    public Guid? TouristId { get; set; }

    [Required]
    [Range(1, 5)]
    public int Score { get; set; }

    [MaxLength(1000)]
    public string? Comment { get; set; }

    [MaxLength(10)]
    public string? LanguageCode { get; set; }

    // Navigation properties
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
    public virtual Tourist? Tourist { get; set; }
}