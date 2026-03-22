using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class PoiContentChangeRequest : BaseEntity
{
    [Required]
    public int OwnerUserId { get; set; }

    [Required]
    public int VendorId { get; set; }

    [Required]
    public int PointOfInterestId { get; set; }

    [Required]
    [MaxLength(20)]
    public string RequestType { get; set; } = "translation"; // translation, audio

    [Required]
    [MaxLength(20)]
    public string ActionType { get; set; } = "update"; // create, update, delete

    public int? AudioContentId { get; set; }

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; set; } = "vi";

    [Required]
    [MaxLength(2000)]
    public string TextContent { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Status { get; set; } = "pending"; // pending, approved, rejected

    public int? ReviewedByUserId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    [MaxLength(500)]
    public string? ReviewNote { get; set; }

    public virtual User OwnerUser { get; set; } = null!;
    public virtual User? ReviewedByUser { get; set; }
    public virtual Vendor Vendor { get; set; } = null!;
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
    public virtual AudioContent? AudioContent { get; set; }
}
