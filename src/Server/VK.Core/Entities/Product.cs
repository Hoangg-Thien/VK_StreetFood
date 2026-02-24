using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class Product : BaseEntity
{
    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Description { get; set; } = string.Empty;

    public decimal Price { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsAvailable { get; set; } = true;

    [Required]
    public int VendorId { get; set; }

    // Navigation properties
    public virtual Vendor Vendor { get; set; } = null!;
}