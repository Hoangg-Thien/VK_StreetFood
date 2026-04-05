using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class PointOfInterestTranslation : BaseEntity
{
    [Required]
    public int PointOfInterestId { get; set; }

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(500)]
    public string Address { get; set; } = string.Empty;

    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
}
