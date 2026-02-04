namespace VK.Shared.DTOs;

public record PointOfInterestDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public double Latitude { get; init; }
    public double Longitude { get; init; }
    public string Address { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public string QRCode { get; init; } = string.Empty;
    public bool IsActive { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? UpdatedAt { get; init; }
    public ICollection<AudioContentDto> AudioContents { get; init; } = new List<AudioContentDto>();
    public ICollection<VendorDto> Vendors { get; init; } = new List<VendorDto>();
}

public record AudioContentDto
{
    public Guid Id { get; init; }
    public Guid PointOfInterestId { get; init; }
    public string LanguageCode { get; init; } = string.Empty;
    public string TextContent { get; init; } = string.Empty;
    public string? AudioFileUrl { get; init; }
    public int DurationInSeconds { get; init; }
    public bool IsGenerated { get; init; }
}

public record VendorDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public string ContactPerson { get; init; } = string.Empty;
    public string PhoneNumber { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? ImageUrl { get; init; }
    public bool IsActive { get; init; }
    public Guid PointOfInterestId { get; init; }
    public ICollection<ProductDto> Products { get; init; } = new List<ProductDto>();
}

public record ProductDto
{
    public Guid Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public decimal Price { get; init; }
    public string? ImageUrl { get; init; }
    public bool IsAvailable { get; init; }
    public Guid VendorId { get; init; }
}

public record TouristDto
{
    public Guid Id { get; init; }
    public string DeviceId { get; init; } = string.Empty;
    public string PreferredLanguage { get; init; } = string.Empty;
    public double? LastLatitude { get; init; }
    public double? LastLongitude { get; init; }
    public DateTime? LastLocationUpdate { get; init; }
}