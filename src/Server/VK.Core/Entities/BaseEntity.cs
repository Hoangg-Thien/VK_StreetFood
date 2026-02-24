using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public abstract class BaseEntity
{
    [Key]
    public int Id { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsDeleted { get; set; } = false;

    public DateTime? DeletedAt { get; set; }
}