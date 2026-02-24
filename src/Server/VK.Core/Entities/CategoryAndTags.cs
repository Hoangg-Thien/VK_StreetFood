using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class Category : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public string? IconUrl { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<PointOfInterest> PointsOfInterest { get; set; } = new List<PointOfInterest>();
}

public class Tag : BaseEntity
{
    [Required]
    [MaxLength(50)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(7)]
    public string ColorCode { get; set; } = "#3B82F6"; // Default blue

    // Navigation properties
    public virtual ICollection<PointOfInterest> PointsOfInterest { get; set; } = new List<PointOfInterest>();
}

public class OpeningHours : BaseEntity
{
    [Required]
    public int VendorId { get; set; }

    [Required]
    [Range(0, 6)] // 0 = Sunday, 6 = Saturday
    public int DayOfWeek { get; set; }

    [Required]
    public TimeSpan OpenTime { get; set; }

    [Required]
    public TimeSpan CloseTime { get; set; }

    public bool IsClosed { get; set; } = false;

    // Navigation properties
    public virtual Vendor Vendor { get; set; } = null!;
}