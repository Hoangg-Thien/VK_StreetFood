using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Core.Interfaces;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ITtsService _ttsService;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        VKStreetFoodDbContext context,
        ITtsService ttsService,
        ILogger<AdminController> logger)
    {
        _context = context;
        _ttsService = ttsService;
        _logger = logger;
    }

    /// <summary>
    /// Batch generate audio files for all POIs in all languages (36 files total)
    /// </summary>
    [HttpPost("generate-all-audio")]
    public async Task<ActionResult> GenerateAllAudio()
    {
        var startTime = DateTime.UtcNow;
        var results = new List<object>();
        var errors = new List<object>();

        try
        {
            _logger.LogInformation("Starting batch audio generation for all POIs");

            // Get all audio contents that need generation
            var audioContents = await _context.AudioContents
                .Include(a => a.PointOfInterest)
                .Where(a => !a.IsDeleted)
                .OrderBy(a => a.PointOfInterestId)
                .ThenBy(a => a.LanguageCode)
                .ToListAsync();

            _logger.LogInformation("Found {Count} audio contents to generate", audioContents.Count);

            foreach (var audio in audioContents)
            {
                try
                {
                    _logger.LogInformation(
                        "Generating audio for POI {PoiId} - {PoiName} ({Language})",
                        audio.PointOfInterestId,
                        audio.PointOfInterest?.Name ?? "Unknown",
                        audio.LanguageCode);

                    // Map language codes
                    var (fullLanguageCode, voiceName) = audio.LanguageCode switch
                    {
                        "vi" => ("vi-VN", "vi-VN-Wavenet-A"),
                        "en" => ("en-US", "en-US-Wavenet-C"),
                        "ko" => ("ko-KR", "ko-KR-Wavenet-A"),
                        _ => ("vi-VN", "vi-VN-Wavenet-A")
                    };

                    // Generate audio
                    var audioFileUrl = await _ttsService.GenerateAudioAsync(
                        audio.TextContent,
                        fullLanguageCode,
                        voiceName);

                    // Update audio record
                    audio.AudioFileUrl = audioFileUrl;
                    audio.DurationInSeconds = _ttsService.EstimateDuration(audio.TextContent);
                    audio.IsGenerated = true;
                    audio.UpdatedAt = DateTime.UtcNow;

                    await _context.SaveChangesAsync();

                    results.Add(new
                    {
                        audioId = audio.Id,
                        poiId = audio.PointOfInterestId,
                        poiName = audio.PointOfInterest?.Name,
                        languageCode = audio.LanguageCode,
                        audioUrl = audioFileUrl,
                        durationInSeconds = audio.DurationInSeconds,
                        success = true
                    });

                    _logger.LogInformation("✅ Generated: {Url}", audioFileUrl);

                    // Small delay to avoid API rate limits
                    await Task.Delay(500);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Failed to generate audio for AudioContent {AudioId}", audio.Id);
                    errors.Add(new
                    {
                        audioId = audio.Id,
                        poiId = audio.PointOfInterestId,
                        languageCode = audio.LanguageCode,
                        error = ex.Message
                    });
                }
            }

            var endTime = DateTime.UtcNow;
            var duration = endTime - startTime;

            _logger.LogInformation(
                "Batch generation completed. Success: {Success}, Failed: {Failed}, Duration: {Duration}s",
                results.Count,
                errors.Count,
                duration.TotalSeconds);

            return Ok(new
            {
                success = true,
                message = $"Generated {results.Count} audio files in {duration.TotalSeconds:F2} seconds",
                totalGenerated = results.Count,
                totalFailed = errors.Count,
                durationSeconds = duration.TotalSeconds,
                results,
                errors
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch audio generation failed");
            return StatusCode(500, new
            {
                success = false,
                message = "Batch generation failed",
                error = ex.Message,
                results,
                errors
            });
        }
    }

    /// <summary>
    /// Get generation status and statistics
    /// </summary>
    [HttpGet("audio-status")]
    public async Task<ActionResult> GetAudioStatus()
    {
        var totalAudioContents = await _context.AudioContents
            .Where(a => !a.IsDeleted)
            .CountAsync();

        var generatedCount = await _context.AudioContents
            .Where(a => !a.IsDeleted && a.IsGenerated)
            .CountAsync();

        var pendingCount = totalAudioContents - generatedCount;

        var byLanguage = await _context.AudioContents
            .Where(a => !a.IsDeleted)
            .GroupBy(a => new { a.LanguageCode, a.IsGenerated })
            .Select(g => new
            {
                languageCode = g.Key.LanguageCode,
                isGenerated = g.Key.IsGenerated,
                count = g.Count()
            })
            .ToListAsync();

        var totalPOIs = await _context.PointsOfInterest
            .Where(p => !p.IsDeleted)
            .CountAsync();

        return Ok(new
        {
            totalPOIs,
            totalAudioContents,
            generatedCount,
            pendingCount,
            percentageComplete = totalAudioContents > 0 
                ? (double)generatedCount / totalAudioContents * 100 
                : 0,
            byLanguage,
            expectedTotal = totalPOIs * 3 // 3 languages per POI
        });
    }

    /// <summary>
    /// Test TTS service connectivity
    /// </summary>
    [HttpGet("test-tts")]
    public async Task<ActionResult> TestTts()
    {
        try
        {
            var testText = "Xin chào, đây là test từ VK Street Food API.";
            
            _logger.LogInformation("Testing TTS service with text: {Text}", testText);

            var audioUrl = await _ttsService.GenerateAudioAsync(
                testText,
                "vi-VN",
                "vi-VN-Wavenet-A");

            var duration = _ttsService.EstimateDuration(testText);

            return Ok(new
            {
                success = true,
                message = "TTS service is working",
                testAudioUrl = audioUrl,
                estimatedDuration = duration,
                testText
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS test failed");
            return StatusCode(500, new
            {
                success = false,
                message = "TTS test failed",
                error = ex.Message,
                hint = "Check GOOGLE_APPLICATION_CREDENTIALS environment variable"
            });
        }
    }
}
