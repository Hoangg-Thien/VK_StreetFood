using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class Tour : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(10)]
    public string Emoji { get; set; } = "🍜";

    public int? EstimatedDurationMinutes { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "draft";

    public virtual ICollection<TourTranslation> Translations { get; set; } = new List<TourTranslation>();
    public virtual ICollection<TourPointOfInterest> TourPoints { get; set; } = new List<TourPointOfInterest>();
}

public class TourPointOfInterest : BaseEntity
{
    public int TourId { get; set; }

    public int PointOfInterestId { get; set; }

    public int SortOrder { get; set; }

    public virtual Tour Tour { get; set; } = null!;

    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
}
