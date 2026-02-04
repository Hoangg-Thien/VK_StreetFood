using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class PointOfInterest : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    public double Latitude { get; set; }

    [Required]
    public double Longitude { get; set; }

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    public string? ImageUrl { get; set; }

    [Required]
    [MaxLength(100)]
    public string QRCode { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    // Navigation properties
    public virtual ICollection<AudioContent> AudioContents { get; set; } = new List<AudioContent>();
    public virtual ICollection<Vendor> Vendors { get; set; } = new List<Vendor>();
}