using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.API.Services;

/// <summary>
/// Singleton service: deduplicates concurrent on-demand TTS generation requests.
/// Nếu cùng (poiId, lang) đang được generate, request thứ 2 join vào task đang chạy
/// thay vì tạo subprocess edge-tts mới — tránh race condition và lãng phí resource.
/// </summary>
public interface IAudioTaskManager
{
    /// <summary>
    /// Trả về audioFileUrl (relative path) nếu MP3 sẵn có hoặc generate thành công.
    /// Trả về null nếu không có AudioContent hoặc generation thất bại.
    /// </summary>
    Task<string?> GetOrGenerateAsync(int poiId, string languageCode, CancellationToken ct = default);
}

public class AudioTaskManager : IAudioTaskManager
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<AudioTaskManager> _logger;
    private readonly string _audioRootPath;

    // Key = "poiId_languageCode" → Task đang chạy (sẽ remove khi xong)
    private readonly ConcurrentDictionary<string, Task<string?>> _pending = new();

    public AudioTaskManager(
        IServiceScopeFactory scopeFactory,
        ILogger<AudioTaskManager> logger,
        IOptions<AudioStorageOptions> audioStorageOptions)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _audioRootPath = audioStorageOptions.Value.RootPath;
    }

    public Task<string?> GetOrGenerateAsync(int poiId, string languageCode, CancellationToken ct = default)
    {
        var key = $"{poiId}_{languageCode.ToLowerInvariant()}";
        // GetOrAdd: nếu task đang chạy thì join, không tạo task mới
        return _pending.GetOrAdd(key, _ => RunAndCleanupAsync(poiId, languageCode, key));
    }

    private async Task<string?> RunAndCleanupAsync(int poiId, string languageCode, string key)
    {
        try
        {
            return await GenerateAsync(poiId, languageCode);
        }
        finally
        {
            // Xóa khỏi dict sau khi xong, cho phép request mới sau đó tạo task mới nếu cần
            _pending.TryRemove(key, out _);
        }
    }

    private async Task<string?> GenerateAsync(int poiId, string languageCode)
    {
        using var scope = _scopeFactory.CreateScope();
        var audioRepository = scope.ServiceProvider.GetRequiredService<IRepository<AudioContent>>();
        var tts = scope.ServiceProvider.GetRequiredService<ITtsGenerationService>();

        var audio = await audioRepository.Query().FirstOrDefaultAsync(a =>
            a.PointOfInterestId == poiId &&
            a.LanguageCode == languageCode &&
            !a.IsDeleted);

        if (audio == null)
        {
            _logger.LogWarning("AudioTaskManager: AudioContent not found for POI {Id} [{Lang}]", poiId, languageCode);
            return null;
        }

        // Đã có file và file còn tồn tại trên storage → trả ngay.
        // Trường hợp deploy/restart làm mất App_Data nhưng DB còn URL cũ, sẽ generate lại.
        if (audio.IsGenerated && !string.IsNullOrEmpty(audio.AudioFileUrl))
        {
            if (IsStoredAudioPresent(audio.AudioFileUrl))
            {
                _logger.LogDebug("AudioTaskManager: cache hit POI {Id} [{Lang}]", poiId, languageCode);
                return audio.AudioFileUrl;
            }

            _logger.LogWarning(
                "AudioTaskManager: stale audio path for POI {Id} [{Lang}] at {Path}, regenerating",
                poiId,
                languageCode,
                audio.AudioFileUrl);
        }

        _logger.LogInformation("AudioTaskManager: generating POI {Id} [{Lang}]", poiId, languageCode);
        var result = await tts.GenerateAsync(audio.Id);

        if (!result.Success)
            _logger.LogWarning("AudioTaskManager: generation failed POI {Id} [{Lang}]: {Err}", poiId, languageCode, result.Error);

        return result.Success ? result.AudioFileUrl : null;
    }

    private bool IsStoredAudioPresent(string? audioFileUrl)
    {
        if (string.IsNullOrWhiteSpace(audioFileUrl))
            return false;

        string normalized = audioFileUrl.Replace('\\', '/').Trim();

        if (Uri.TryCreate(audioFileUrl, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                || absolute.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (absolute.Scheme.Equals(Uri.UriSchemeFile, StringComparison.OrdinalIgnoreCase))
            {
                normalized = absolute.AbsolutePath;
            }
            else
            {
                return false;
            }
        }

        if (normalized.StartsWith("/audio/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["/audio/".Length..];
        else if (normalized.StartsWith("audio/", StringComparison.OrdinalIgnoreCase))
            normalized = normalized["audio/".Length..];

        if (string.IsNullOrWhiteSpace(normalized))
            return false;

        var relativePath = normalized
            .TrimStart('/')
            .Replace('/', Path.DirectorySeparatorChar);

        var fullPath = Path.Combine(_audioRootPath, relativePath);
        return File.Exists(fullPath);
    }
}
