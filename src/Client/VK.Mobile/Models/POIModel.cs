namespace VK.Mobile.Models;

public class POIModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public string Address { get; set; } = string.Empty;
    public string? QrCode { get; set; }
    public string? ImageUrl { get; set; }
    public string? CategoryName { get; set; }
    public int ViewCount { get; set; }
    public double? AverageRating { get; set; }
    public double DistanceKm { get; set; }

    // Audio info
    public AudioInfo? Audio { get; set; }
}

public class AudioInfo
{
    public int Id { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? AudioFileUrl { get; set; }
    public int? DurationSeconds { get; set; }
}

public class POIDetailModel : POIModel
{
    public string? CategoryDescription { get; set; }
    public List<AudioInfo> AudioContents { get; set; } = new();
    public List<VendorInfo> Vendors { get; set; } = new();
    public List<RatingInfo> Ratings { get; set; } = new();
}

public class VendorInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ContactPhone { get; set; }
    public List<ProductInfo> Products { get; set; } = new();
}

public class ProductInfo
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public string? ImageUrl { get; set; }
}

public class RatingInfo
{
    public int Id { get; set; }
    public int RatingValue { get; set; }
    public string? Comment { get; set; }
    public DateTime CreatedAt { get; set; }
}
