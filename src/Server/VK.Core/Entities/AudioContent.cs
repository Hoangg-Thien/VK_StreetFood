using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class AudioContent : BaseEntity
{
    [Required]
    public int PointOfInterestId { get; set; }

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = string.Empty; // vi, en, ko

    [Required]
    [MaxLength(2000)]
    public string TextContent { get; set; } = string.Empty;

    public string? AudioFileUrl { get; set; }

    public int DurationInSeconds { get; set; }

    public bool IsGenerated { get; set; } = false;

    // Navigation properties
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
}