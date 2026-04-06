namespace VK.Shared.DTOs;

public class TourListItemDto
{
    public int TourId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Emoji { get; set; } = "🍜";
    public int? EstimatedDurationMinutes { get; set; }
    public string Status { get; set; } = "draft";
    public int StopsCount { get; set; }
    public int? FirstPoiId { get; set; }
    public string? CoverImageUrl { get; set; }
}

public class TourDetailDto : TourListItemDto
{
    public List<TourPointDto> Points { get; set; } = new();
}

public class TourPointDto
{
    public int PoiId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int SortOrder { get; set; }
}