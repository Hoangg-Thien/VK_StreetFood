using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Interfaces;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<AudioController> _logger;
    private readonly IWebHostEnvironment _environment;
    private readonly ITtsService _ttsService;

    public AudioController(
        VKStreetFoodDbContext context, 
        ILogger<AudioController> logger,
        IWebHostEnvironment environment,
        ITtsService ttsService)
    {
        _context = context;
        _logger = logger;
        _environment = environment;
        _ttsService = ttsService;
    }

    /// <summary>
    /// Get audio content for a POI in specific language
    /// </summary>
    [HttpGet("poi/{poiId}")]
    public async Task<ActionResult> GetAudioByPOI(int poiId, [FromQuery] string languageCode = "vi")
    {
        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a => 
                a.PointOfInterestId == poiId && 
                a.LanguageCode == languageCode &&
                !a.IsDeleted);

        // Fallback to Vietnamese if not found
        if (audio == null)
        {
            audio = await _context.AudioContents
                .FirstOrDefaultAsync(a => 
                    a.PointOfInterestId == poiId && 
                    a.LanguageCode == "vi" &&
                    !a.IsDeleted);
        }

        if (audio == null)
        {
            return NotFound(new { message = "Audio không tồn tại" });
        }

        return Ok(new
        {
            audioId = audio.Id,
            poiId = audio.PointOfInterestId,
            languageCode = audio.LanguageCode,
            audioFileUrl = audio.AudioFileUrl,
            textContent = audio.TextContent,
            durationInSeconds = audio.DurationInSeconds,
            isGenerated = audio.IsGenerated
        });
    }

    /// <summary>
    /// Stream audio file
    /// </summary>
    [HttpGet("stream/{audioId}")]
    public async Task<ActionResult> StreamAudio(int audioId)
    {
        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a => a.Id == audioId && !a.IsDeleted);

        if (audio == null)
        {
            return NotFound(new { message = "Audio không tồn tại" });
        }

        // For now, return the URL for client to stream
        // In production, you would implement actual file streaming from storage
        return Ok(new
        {
            streamUrl = audio.AudioFileUrl,
            contentType = "audio/mpeg",
            durationInSeconds = audio.DurationInSeconds
        });
    }

    /// <summary>
    /// Get all available languages for a POI
    /// </summary>
    [HttpGet("poi/{poiId}/languages")]
    public async Task<ActionResult> GetAvailableLanguages(int poiId)
    {
        var languages = await _context.AudioContents
            .Where(a => a.PointOfInterestId == poiId && !a.IsDeleted)
            .Select(a => new
            {
                languageCode = a.LanguageCode,
                languageName = GetLanguageName(a.LanguageCode),
                durationInSeconds = a.DurationInSeconds,
                isGenerated = a.IsGenerated
            })
            .ToListAsync();

        return Ok(languages);
    }

    /// <summary>
    /// Generate audio using Google Cloud Text-to-Speech
    /// </summary>
    [HttpPost("generate")]
    public async Task<ActionResult> GenerateAudio([FromBody] GenerateAudioRequest request)
    {
        try
        {
            var audio = await _context.AudioContents
                .FirstOrDefaultAsync(a => 
                    a.PointOfInterestId == request.PoiId && 
                    a.LanguageCode == request.LanguageCode &&
                    !a.IsDeleted);

            if (audio == null)
            {
                return NotFound(new { message = "Audio content không tồn tại" });
            }

            // Map language codes to full locale and voice names
            var (fullLanguageCode, defaultVoiceName) = request.LanguageCode switch
            {
                "vi" => ("vi-VN", "vi-VN-Wavenet-A"),
                "en" => ("en-US", "en-US-Wavenet-C"),
                "ko" => ("ko-KR", "ko-KR-Wavenet-A"),
                _ => ("vi-VN", "vi-VN-Wavenet-A")
            };

            var voiceName = request.VoiceName ?? defaultVoiceName;

            _logger.LogInformation(
                "Generating audio for POI {PoiId}, Language {Language}, Voice {Voice}", 
                request.PoiId, request.LanguageCode, voiceName);

            // Generate audio using Google Cloud TTS
            var audioFileUrl = await _ttsService.GenerateAudioAsync(
                audio.TextContent, 
                fullLanguageCode, 
                voiceName);

            // Estimate duration
            var estimatedDuration = _ttsService.EstimateDuration(audio.TextContent);

            // Update audio record
            audio.AudioFileUrl = audioFileUrl;
            audio.DurationInSeconds = estimatedDuration;
            audio.IsGenerated = true;
            audio.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            _logger.LogInformation("Audio generated successfully: {Url}", audioFileUrl);

            return Ok(new
            {
                success = true,
                message = "Audio generated successfully",
                audioId = audio.Id,
                audioUrl = audioFileUrl,
                durationInSeconds = estimatedDuration,
                languageCode = request.LanguageCode,
                voiceUsed = voiceName
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio for POI {PoiId}", request.PoiId);
            return StatusCode(500, new { message = "Lỗi khi tạo audio", error = ex.Message });
        }
    }

    private string GetLanguageName(string languageCode)
    {
        return languageCode switch
        {
            "vi" => "Tiếng Việt",
            "en" => "English",
            "ko" => "한국어 (Korean)",
            _ => languageCode
        };
    }
}

public class GenerateAudioRequest
{
    public int PoiId { get; set; }
    public string LanguageCode { get; set; } = "vi";
    public string? VoiceName { get; set; }
}
