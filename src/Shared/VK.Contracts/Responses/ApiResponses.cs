using VK.Shared.DTOs;

namespace VK.Contracts.Responses;

public record ApiResponse<T>
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public T? Data { get; init; }
    public IEnumerable<string> Errors { get; init; } = Array.Empty<string>();
}

public record PointOfInterestResponse
{
    public POIListItemDto PointOfInterest { get; init; } = null!;
    public double DistanceInMeters { get; init; }
}

public record QRCodeScanResponse
{
    public AudioContentDto? AudioContent { get; init; }
    public bool IsNearby { get; init; }
    public double DistanceInMeters { get; init; }
}

public record NearbyPointsResponse
{
    public IEnumerable<PointOfInterestResponse> Points { get; init; } = Array.Empty<PointOfInterestResponse>();
    public int TotalCount { get; init; }
}

