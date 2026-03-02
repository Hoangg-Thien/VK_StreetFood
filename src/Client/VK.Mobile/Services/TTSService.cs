using Microsoft.Extensions.Logging;
using System.Net.Http.Json;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITTSService
{
    /// <summary>
    /// Phát thuyết minh cho POI:
    ///   1. Nếu có file audio sẵn → play qua AudioService (giọng chuyên nghiệp)
    ///   2. Nếu không → gọi backend generate TTS → download và play
    ///   3. Cuối cùng fallback MAUI TextToSpeech (built-in, nhẹ, không cần network riêng)
    /// </summary>
    Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default);

    /// <summary>Phát thẳng text bằng MAUI TTS theo ngôn ngữ chỉ định.</summary>
    Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default);

    /// <summary>Dừng mọi phát âm đang chạy.</summary>
    Task StopAsync();
}

public class TTSService : ITTSService
{
    private readonly IAudioService _audioService;
    private readonly HttpClient _httpClient;
    private readonly ILogger<TTSService> _logger;
    private CancellationTokenSource? _ttsCts;

    // Mapping language code → Google TTS voice name (WaveNet = chất lượng cao)
    private static readonly Dictionary<string, string> VoiceMap = new()
    {
        { "vi", "vi-VN-Wavenet-A" },
        { "en", "en-US-Wavenet-D" },
        { "ko", "ko-KR-Wavenet-A" }
    };

    public TTSService(IAudioService audioService, HttpClient httpClient, ILogger<TTSService> logger)
    {
        _audioService = audioService;
        _httpClient = httpClient;
        _httpClient.BaseAddress = new Uri(AppSettings.ApiBaseUrl);
        _logger = logger;
    }

    public async Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default)
    {
        // Hủy TTS đang chạy (nếu có)
        _ttsCts?.Cancel();
        _ttsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        // ── Tầng 1: File audio thu sẵn ───────────────────────────────
        var audioUrl = poi.Audio?.AudioFileUrl;
        if (!string.IsNullOrWhiteSpace(audioUrl))
        {
            _logger.LogInformation("TTS Layer 1: playing pre-recorded audio for POI {Id}", poi.Id);
            var ok = await _audioService.PlayAudioAsync(audioUrl);
            if (ok) return;
            _logger.LogWarning("Pre-recorded audio failed, falling back to TTS");
        }

        // ── Tầng 2: Backend Google Cloud TTS → download MP3 → play ───
        try
        {
            _logger.LogInformation("TTS Layer 2: requesting TTS from backend for POI {Id}", poi.Id);

            var voiceName = VoiceMap.GetValueOrDefault(languageCode, "vi-VN-Wavenet-A");
            var response = await _httpClient.PostAsJsonAsync(
                "audio/generate",
                new { poiId = poi.Id, languageCode, voiceName },
                _ttsCts.Token);

            if (response.IsSuccessStatusCode)
            {
                var result = await response.Content.ReadFromJsonAsync<GeneratedAudioResult>(cancellationToken: ct);
                if (result?.AudioFileUrl != null)
                {
                    var ok = await _audioService.PlayAudioAsync(result.AudioFileUrl);
                    if (ok) return;
                }
            }
            else
            {
                _logger.LogWarning("Backend TTS returned {Status}, falling back to MAUI TTS", response.StatusCode);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Backend TTS failed, falling back to MAUI built-in TTS");
        }

        // ── Tầng 3: MAUI built-in TextToSpeech (offline, nhẹ) ────────
        try
        {
            _logger.LogInformation("TTS Layer 3: using MAUI TextToSpeech for POI {Id}", poi.Id);

            var text = string.IsNullOrWhiteSpace(poi.Description)
                ? poi.Name
                : $"{poi.Name}. {poi.Description[..Math.Min(300, poi.Description.Length)]}";

            await SpeakWithMauiTtsAsync(text, languageCode, _ttsCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "All TTS layers failed for POI {Id}", poi.Id);
        }
    }

    public async Task StopAsync()
    {
        _ttsCts?.Cancel();
        _ttsCts = null;
        await _audioService.StopAsync();
        // Cancel token dừng SpeakAsync đang chạy
    }

    public async Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default)
    {
        _ttsCts?.Cancel();
        _ttsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        try
        {
            await SpeakWithMauiTtsAsync(text, languageCode, _ttsCts.Token);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SpeakTextAsync failed");
        }
    }

    private async Task SpeakWithMauiTtsAsync(string text, string languageCode, CancellationToken ct)
    {
        _logger.LogInformation("MAUI TTS: lang={Lang}, text=\"{Text}\"", languageCode, text[..Math.Min(60, text.Length)]);
        System.Diagnostics.Debug.WriteLine($"[TTS] Speaking ({languageCode}): {text[..Math.Min(80, text.Length)]}");

        // GetLocalesAsync + SpeakAsync phải chạy trên MainThread trên Android
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var locales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();

                System.Diagnostics.Debug.WriteLine($"[TTS] Available locales: {string.Join(", ", locales.Select(l => $"{l.Language}-{l.Country}"))}");

                Locale? matched = languageCode switch
                {
                    "en" => locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    "ko" => locales.FirstOrDefault(l => l.Language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    _ => locales.FirstOrDefault(l => l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault()
                };

                System.Diagnostics.Debug.WriteLine($"[TTS] Locale matched: {matched?.Language ?? "null"}-{matched?.Country ?? "null"}");

                var options = new SpeechOptions { Locale = matched, Pitch = 1.0f, Volume = 1.0f };
                await TextToSpeech.Default.SpeakAsync(text, options, ct);

                System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync completed");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] MainThread speak error: {ex.Message}");
                _logger.LogError(ex, "TTS speak failed on MainThread");
            }
        });
    }

    private record GeneratedAudioResult(string? AudioFileUrl);
}