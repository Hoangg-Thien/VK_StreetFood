using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.API.Services;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AudioController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ITtsGenerationService _tts;
    private readonly IAudioTaskManager _taskManager;
    private readonly ILogger<AudioController> _logger;

    public AudioController(
        VKStreetFoodDbContext context,
        ITtsGenerationService tts,
        IAudioTaskManager taskManager,
        ILogger<AudioController> logger)
    {
        _context = context;
        _tts = tts;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <summary>
    /// Lấy nội dung audio cho POI theo ngôn ngữ.
    /// Trả về audioFileUrl nếu MP3 đã được generate, ngược lại trả textContent để MAUI TTS đọc.
    /// Fallback về tiếng Việt nếu chưa có bản dịch.
    /// </summary>
    [HttpGet("poi/{poiId}")]
    public async Task<ActionResult> GetAudioByPOI(int poiId, [FromQuery] string languageCode = "vi")
    {
        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a =>
                a.PointOfInterestId == poiId &&
                a.LanguageCode == languageCode &&
                !a.IsDeleted);

        // 3-Tier Content Fallback: target lang → vi (Vietnamese)
        if (audio == null && languageCode != "vi")
        {
            audio = await _context.AudioContents
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == poiId &&
                    a.LanguageCode == "vi" &&
                    !a.IsDeleted);
        }

        if (audio == null)
            return NotFound(new { message = "Không có nội dung thuyết minh cho điểm này" });

        _logger.LogInformation("Audio served: POI {Id}, lang={Lang}, generated={Gen}",
            poiId, audio.LanguageCode, audio.IsGenerated);

        return Ok(new
        {
            audioId = audio.Id,
            poiId = audio.PointOfInterestId,
            languageCode = audio.LanguageCode,
            textContent = audio.TextContent,
            audioFileUrl = audio.AudioFileUrl != null
                                ? $"{Request.Scheme}://{Request.Host}{audio.AudioFileUrl}"
                                : null,
            isGenerated = audio.IsGenerated,
            durationSeconds = audio.DurationSeconds,
            isFallback = !audio.LanguageCode.Equals(
                languageCode.Trim().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase)
        });
    }

    [HttpGet("poi/{poiId}/languages")]
    public async Task<ActionResult> GetAvailableLanguages(int poiId)
    {
        var languages = await _context.AudioContents
            .Where(a => a.PointOfInterestId == poiId && !a.IsDeleted)
            .Select(a => new
            {
                languageCode = a.LanguageCode,
                languageName = a.LanguageCode == "vi" ? "Tiếng Việt"
                             : a.LanguageCode == "en" ? "English"
                             : a.LanguageCode == "ko" ? "한국어"
                             : a.LanguageCode,
                isGenerated = a.IsGenerated
            })
            .ToListAsync();

        return Ok(languages);
    }

    /// <summary>
    /// [Tourist] On-demand TTS: trả về audioFileUrl cho POI theo ngôn ngữ.
    /// Nếu MP3 đã có → trả ngay. Nếu chưa → generate via edge-tts qua AudioTaskManager.
    /// AudioTaskManager deduplicates: 2 requests cùng POI/lang chỉ chạy 1 subprocess.
    /// </summary>
    [HttpPost("tts")]
    public async Task<ActionResult> GetOrGenerateTts(
        [FromBody] OnDemandTtsRequest request,
        CancellationToken ct)
    {
        var lang = string.IsNullOrWhiteSpace(request.LanguageCode)
            ? "vi"
            : request.LanguageCode.Trim().ToLowerInvariant();

        var audio = await _context.AudioContents
            .FirstOrDefaultAsync(a =>
                a.PointOfInterestId == request.PoiId &&
                a.LanguageCode == lang &&
                !a.IsDeleted, ct);

        // Fallback về tiếng Việt nếu chưa có bản ngôn ngữ yêu cầu
        if (audio == null && lang != "vi")
        {
            audio = await _context.AudioContents
                .FirstOrDefaultAsync(a =>
                    a.PointOfInterestId == request.PoiId &&
                    a.LanguageCode == "vi" &&
                    !a.IsDeleted, ct);
            if (audio != null) lang = "vi";
        }

        if (audio == null)
            return NotFound(new { message = "Không có nội dung thuyết minh cho điểm này" });

        // Nếu chưa có file → dùng AudioTaskManager (deduplicates concurrent requests)
        if (!audio.IsGenerated || string.IsNullOrEmpty(audio.AudioFileUrl))
        {
            var generatedUrl = await _taskManager.GetOrGenerateAsync(
                audio.PointOfInterestId, audio.LanguageCode, ct);
            if (generatedUrl != null)
                await _context.Entry(audio).ReloadAsync(ct);
        }

        _logger.LogInformation(
            "On-demand TTS: POI {Id} [{Lang}] → generated={Gen}",
            request.PoiId, audio.LanguageCode, audio.IsGenerated);

        return Ok(new
        {
            audioId = audio.Id,
            poiId = audio.PointOfInterestId,
            languageCode = audio.LanguageCode,
            textContent = audio.TextContent,
            audioFileUrl = audio.AudioFileUrl != null
                                ? $"{Request.Scheme}://{Request.Host}{audio.AudioFileUrl}"
                                : null,
            isGenerated = audio.IsGenerated,
            durationSeconds = audio.DurationSeconds,
            isFallback = !audio.LanguageCode.Equals(lang, StringComparison.OrdinalIgnoreCase)
        });
    }

    /// <summary>
    /// [Admin] Generate MP3 cho 1 POI (tất cả ngôn ngữ).
    /// </summary>
    [HttpPost("generate/poi/{poiId}")]
    public async Task<ActionResult> GenerateForPoi(int poiId, CancellationToken ct)
    {
        var exists = await _context.PointsOfInterest
            .AnyAsync(p => p.Id == poiId && !p.IsDeleted, ct);

        if (!exists)
            return NotFound(new { message = "POI không tồn tại" });

        var results = await _tts.GenerateForPoiAsync(poiId, ct);

        return Ok(new
        {
            poiId,
            total = results.Count,
            succeeded = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            results
        });
    }

    /// <summary>
    /// [Admin] Generate MP3 cho toàn bộ AudioContent chưa có file (chạy 1 lần khi setup).
    /// </summary>
    [HttpPost("generate/all")]
    public async Task<ActionResult> GenerateAll(CancellationToken ct)
    {
        var missing = await _context.AudioContents
            .CountAsync(a => !a.IsDeleted && !a.IsGenerated, ct);

        if (missing == 0)
            return Ok(new { message = "Tất cả audio đã được generate rồi.", total = 0 });

        _logger.LogInformation("Starting bulk TTS generation for {Count} items", missing);
        var results = await _tts.GenerateAllMissingAsync(ct);

        return Ok(new
        {
            total = results.Count,
            succeeded = results.Count(r => r.Success),
            failed = results.Count(r => !r.Success),
            errors = results.Where(r => !r.Success)
                               .Select(r => new { r.PoiId, r.LanguageCode, r.Error })
        });
    }

    /// <summary>
    /// [Admin] Trạng thái generate audio — bao nhiêu cái đã có MP3, bao nhiêu chưa.
    /// </summary>
    [HttpGet("generate/status")]
    public async Task<ActionResult> GetGenerateStatus()
    {
        var all = await _context.AudioContents
            .Where(a => !a.IsDeleted)
            .GroupBy(a => a.LanguageCode)
            .Select(g => new
            {
                language = g.Key,
                total = g.Count(),
                generated = g.Count(a => a.IsGenerated),
                missing = g.Count(a => !a.IsGenerated)
            })
            .ToListAsync();

        return Ok(new
        {
            summary = all,
            totalGenerated = all.Sum(x => x.generated),
            totalMissing = all.Sum(x => x.missing)
        });
    }
}

public record OnDemandTtsRequest(int PoiId, string LanguageCode = "vi");
