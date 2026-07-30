using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json.Serialization;
using VK.API.Services;
using VK.Infrastructure.Data;

namespace VK.API.Controllers;

/// <summary>
/// Module Localization — chuẩn bị nội dung âm thanh trước khi tourist cần.
/// Gồm 2 chiến lược song song với AudioTaskManager:
///   - Hotset  : pre-warm top ~10 POI gần nhất ngay khi mở app (2-3s GPS lock)
///   - Warmup  : generate full corpus dưới nền, đảm bảo mọi POI đều có MP3 offline
/// </summary>
[ApiController]
[Route("api/localizations")]
public class LocalizationController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly IAudioTaskManager _taskManager;
    private readonly ILogger<LocalizationController> _logger;

    public LocalizationController(
        VKStreetFoodDbContext context,
        IAudioTaskManager taskManager,
        ILogger<LocalizationController> logger)
    {
        _context = context;
        _taskManager = taskManager;
        _logger = logger;
    }

    /// <summary>
    /// Hotset: Nhận danh sách poiIds (≤10) + lang → check và pre-generate MP3 còn thiếu.
    /// Mobile gọi ngay sau khi có GPS lock, fire-and-forget trên server.
    /// Trả về ngay lập tức (không chờ generation hoàn thành).
    /// </summary>
    [HttpPost("prepare-hotset")]
    public async Task<ActionResult> PrepareHotset([FromBody] HotsetRequest request, CancellationToken ct)
    {
        if (request.POIIds == null || request.POIIds.Count == 0)
            return BadRequest(new { message = "poiIds không được để trống" });

        var lang = NormalizeLang(request.LanguageCode);

        // Giới hạn batch để tránh DDoS ngay cả từ app
        var poiIds = request.POIIds.Distinct().Take(10).ToList();

        // Tìm AudioContent chưa generate cho những POI này
        var missing = await _context.AudioContents
            .Where(a =>
                poiIds.Contains(a.PointOfInterestId) &&
                a.LanguageCode == lang &&
                !a.IsDeleted &&
                !a.IsGenerated)
            .Select(a => new { a.PointOfInterestId, a.LanguageCode })
            .ToListAsync(ct);

        // Fire-and-forget tất cả — AudioTaskManager deduplicates
        var queued = 0;
        foreach (var item in missing)
        {
            _ = _taskManager.GetOrGenerateAsync(item.PointOfInterestId, item.LanguageCode);
            queued++;
        }

        _logger.LogInformation(
            "Hotset: lang={Lang}, requested={Req}, queued={Q}",
            lang, poiIds.Count, queued);

        return Ok(new
        {
            requested = poiIds.Count,
            alreadyGenerated = poiIds.Count - missing.Count,
            queued,
            language = lang
        });
    }

    /// <summary>
    /// Warmup: Generate toàn bộ AudioContent chưa có MP3 cho lang yêu cầu.
    /// Chạy hoàn toàn dưới nền (fire-and-forget). Trả về ngay thống kê.
    /// Dùng sau khi user chọn ngôn ngữ hoặc từ SettingsPage download offline.
    /// </summary>
    [HttpPost("warmup")]
    public async Task<ActionResult> Warmup([FromBody] WarmupRequest request, CancellationToken ct)
    {
        var lang = NormalizeLang(request.LanguageCode);

        var missing = await _context.AudioContents
            .Where(a =>
                a.LanguageCode == lang &&
                !a.IsDeleted &&
                !a.IsGenerated)
            .Select(a => new { a.PointOfInterestId, a.LanguageCode })
            .ToListAsync(ct);

        if (missing.Count == 0)
        {
            _logger.LogInformation("Warmup: lang={Lang} — tất cả đã generate", lang);
            return Ok(new { language = lang, total = 0, queued = 0, message = "Tất cả audio đã sẵn sàng." });
        }

        // Fire-and-forget toàn bộ — AudioTaskManager giới hạn concurrency
        foreach (var item in missing)
            _ = _taskManager.GetOrGenerateAsync(item.PointOfInterestId, item.LanguageCode);

        _logger.LogInformation("Warmup: lang={Lang}, queued={Q} items", lang, missing.Count);

        return Ok(new
        {
            language = lang,
            total = missing.Count,
            queued = missing.Count,
            message = $"Đã enqueue {missing.Count} audio để generate dưới nền."
        });
    }

    private static string NormalizeLang(string? lang)
        => string.IsNullOrWhiteSpace(lang) ? "vi" : lang.Trim().ToLowerInvariant();
}

public class HotsetRequest
{
    [Required]
    [JsonPropertyName("poiIds")]
    public List<int> POIIds { get; set; } = new();

    [MaxLength(10)]
    public string LanguageCode { get; set; } = "vi";
}

public class WarmupRequest
{
    [MaxLength(10)]
    public string LanguageCode { get; set; } = "vi";
}
