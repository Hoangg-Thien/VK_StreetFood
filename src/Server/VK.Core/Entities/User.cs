using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class User : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? FullName { get; set; }

    [MaxLength(500)]
    public string? AvatarUrl { get; set; }

    [Required]
    [MaxLength(20)]
    public string Role { get; set; } = "Tourist"; // Tourist, Vendor, Admin

    public int? VendorId { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual Vendor? Vendor { get; set; }
}

public class Favorite : BaseEntity
{
    [Required]
    public int TouristId { get; set; }
    
    [Required]
    public int PointOfInterestId { get; set; }
    [MaxLength(500)]
    public string? Note { get; set; }

    // Navigation properties
    public virtual Tourist Tourist { get; set; } = null!;
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
}