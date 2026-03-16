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

    /// <summary>Đường dẫn file MP3 pre-generated, VD: /audio/vi/poi_1.mp3. Null nếu chưa generate.</summary>
    [MaxLength(500)]
    public string? AudioFileUrl { get; set; }

    /// <summary>True nếu MP3 đã được generate và file tồn tại trên disk.</summary>
    public bool IsGenerated { get; set; } = false;

    /// <summary>Thời gian audio (giây), null nếu chưa generate.</summary>
    public int? DurationSeconds { get; set; }

    // Navigation properties
    public virtual PointOfInterest PointOfInterest { get; set; } = null!;
}