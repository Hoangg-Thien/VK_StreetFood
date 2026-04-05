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

    [MaxLength(500)]
    public string? PasswordHash { get; set; }

    public bool IsVerified { get; set; } = false;

    public int? VendorId { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation properties
    public virtual Vendor? Vendor { get; set; }
    public virtual ICollection<PoiOwnerRegistration> OwnerRegistrations { get; set; } = new List<PoiOwnerRegistration>();
}

public class PoiOwnerRegistration : BaseEntity
{
    [Required]
    public int UserId { get; set; }

    public int? PointOfInterestId { get; set; }

    public int? VendorId { get; set; }

    [Required]
    [MaxLength(200)]
    public string ShopName { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? ShopAddress { get; set; }

    [MaxLength(15)]
    public string? ContactPhone { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, approved, rejected

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(500)]
    public string? ReviewNote { get; set; }

    public virtual User User { get; set; } = null!;
    public virtual PointOfInterest? PointOfInterest { get; set; }
    public virtual Vendor? Vendor { get; set; }
    public virtual User? ReviewedByUser { get; set; }
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