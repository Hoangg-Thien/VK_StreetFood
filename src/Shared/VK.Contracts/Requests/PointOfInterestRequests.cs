using System.ComponentModel.DataAnnotations;

namespace VK.Contracts.Requests;

public record CreatePointOfInterestRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; init; }

    [MaxLength(500)]
    public string Address { get; init; } = string.Empty;

    public string? ImageUrl { get; init; }
}

public record UpdatePointOfInterestRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(1000)]
    public string Description { get; init; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; init; }

    [MaxLength(500)]
    public string Address { get; init; } = string.Empty;

    public string? ImageUrl { get; init; }

    public bool IsActive { get; init; } = true;
}

public record QRCodeScanRequest
{
    [Required]
    [MaxLength(100)]
    public string QRCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(100)]
    public string DeviceId { get; init; } = string.Empty;

    [Required]
    [Range(-90, 90)]
    public double Latitude { get; init; }

    [Required]
    [Range(-180, 180)]
    public double Longitude { get; init; }

    [MaxLength(10)]
    public string LanguageCode { get; init; } = "vi";
}