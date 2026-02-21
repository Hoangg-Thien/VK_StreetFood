using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class Vendor : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string ContactPerson { get; set; } = string.Empty;

    [Required]
    [MaxLength(15)]
    public string PhoneNumber { get; set; } = string.Empty;

    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Required]
    public Guid PointOfInterestId { get; set; }

    public decimal AverageRating { get; set; } = 0;

    public int TotalReviews { get; set; } = 0;

    // Navigation properties
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
    public virtual ICollection<Product> Products { get; set; } = new List<Product>();
    public virtual ICollection<OpeningHours> OpeningHours { get; set; } = new List<OpeningHours>();
    public virtual ICollection<User> Users { get; set; } = new List<User>();
}