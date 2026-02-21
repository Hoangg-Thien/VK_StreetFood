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

    public Guid? UserId { get; set; }

    public int TotalVisits { get; set; } = 0;

    // Navigation properties
    public virtual User? User { get; set; }
    public virtual ICollection<VisitLog> VisitLogs { get; set; } = new List<VisitLog>();
    public virtual ICollection<Favorite> Favorites { get; set; } = new List<Favorite>();
    public virtual ICollection<Rating> Ratings { get; set; } = new List<Rating>();
    public virtual ICollection<Analytics> Analytics { get; set; } = new List<Analytics>();
}