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
    public PointOfInterestDto PointOfInterest { get; init; } = null!;
    public double DistanceInMeters { get; init; }
}

public record QRCodeScanResponse
{
    public PointOfInterestDto PointOfInterest { get; init; } = null!;
    public AudioContentDto? AudioContent { get; init; }
    public bool IsNearby { get; init; }
    public double DistanceInMeters { get; init; }
}

public record NearbyPointsResponse
{
    public IEnumerable<PointOfInterestResponse> Points { get; init; } = Array.Empty<PointOfInterestResponse>();
    public int TotalCount { get; init; }
}

public record AudioGenerationResponse
{
    public string AudioFileUrl { get; init; } = string.Empty;
    public int DurationInSeconds { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
}