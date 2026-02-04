using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class Tourist : BaseEntity
{
    [Required]
    [MaxLength(100)]
    public string DeviceId { get; set; } = string.Empty;

    [MaxLength(10)]
    public string PreferredLanguage { get; set; } = "vi"; // Default Vietnamese

    public double? LastLatitude { get; set; }

    public double? LastLongitude { get; set; }

    public DateTime? LastLocationUpdate { get; set; }

    // Navigation properties
    public virtual ICollection<VisitLog> VisitLogs { get; set; } = new List<VisitLog>();
}