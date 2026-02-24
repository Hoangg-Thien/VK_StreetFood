namespace VK.Shared.DTOs;

// Shared DTOs used across multiple controllers

public class AudioContentDto
{
    public int AudioId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string AudioFileUrl { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public int DurationInSeconds { get; set; }
}

public class VendorDto
{
    public int VendorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal AverageRating { get; set; }
    public List<ProductDto> Products { get; set; } = new();
}

public class ProductDto
{
    public int ProductId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}

public class TouristDto
{
    public int TouristId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "vi";
    public int TotalVisits { get; set; }
}
