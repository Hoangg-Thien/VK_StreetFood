using System.ComponentModel.DataAnnotations;

namespace VK.Contracts.Requests;

public record CreateAudioContentRequest
{
    [Required]
    public Guid PointOfInterestId { get; init; }

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; init; } = string.Empty;

    [Required]
    [MaxLength(2000)]
    public string TextContent { get; init; } = string.Empty;

    public bool GenerateAudio { get; init; } = true;
}

public record UpdateAudioContentRequest
{
    [Required]
    [MaxLength(2000)]
    public string TextContent { get; init; } = string.Empty;

    public bool RegenerateAudio { get; init; } = false;
}

public record GenerateAudioRequest
{
    [Required]
    [MaxLength(2000)]
    public string Text { get; init; } = string.Empty;

    [Required]
    [MaxLength(10)]
    public string LanguageCode { get; init; } = string.Empty;
}