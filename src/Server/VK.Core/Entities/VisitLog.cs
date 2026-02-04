using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class VisitLog : BaseEntity
{
    [Required]
    public Guid TouristId { get; set; }

    [Required]
    public Guid PointOfInterestId { get; set; }

    public DateTime VisitedAt { get; set; } = DateTime.UtcNow;

    public double VisitorLatitude { get; set; }

    public double VisitorLongitude { get; set; }

    [MaxLength(10)]
    public string LanguageUsed { get; set; } = string.Empty;

    public bool AudioPlayed { get; set; } = false;

    // Navigation properties
    public virtual Tourist Tourist { get; set; } = null!;
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
}