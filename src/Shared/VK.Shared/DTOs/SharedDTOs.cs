namespace VK.Shared.DTOs;

// Shared DTOs used across multiple controllers

public class AudioContentDto
{
    public int AudioId { get; set; }
    public string LanguageCode { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
    public string? AudioFileUrl { get; set; }
    public bool IsGenerated { get; set; }
    public int? DurationSeconds { get; set; }
}

public class VendorDto
{
    public int VendorId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? PhoneNumber { get; set; }
    public decimal AverageRating { get; set; }
}

public class TouristDto
{
    public int TouristId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string PreferredLanguage { get; set; } = "vi";
    public int TotalVisits { get; set; }

    /// <summary>
    /// JWT bearer token. Populated ONLY on the initial register response.
    /// The client must store this securely and attach it as Authorization: Bearer {Token}
    /// on all subsequent tourist-scoped API calls.
    /// </summary>
    public string? Token { get; set; }
}
