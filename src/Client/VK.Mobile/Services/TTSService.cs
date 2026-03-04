using Microsoft.Extensions.Logging;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITTSService
{
    /// <summary>Phat thuyet minh cho POI bang MAUI TextToSpeech.</summary>
    Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default);

    /// <summary>Phat thang text bang MAUI TTS theo ngon ngu chi dinh.</summary>
    Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default);

    /// <summary>Dung moi phat am dang chay.</summary>
    Task StopAsync();
}

public class TTSService : ITTSService
{
    private readonly ILogger<TTSService> _logger;
    private CancellationTokenSource? _ttsCts;

    public TTSService(ILogger<TTSService> logger)
    {
        _logger = logger;
    }

    public async Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default)
    {
        var text = string.IsNullOrWhiteSpace(poi.Description)
            ? poi.Name
            : $"{poi.Name}. {poi.Description}";
        await SpeakTextAsync(text, languageCode, ct);
    }

    public async Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;

        _ttsCts?.Cancel();
        _ttsCts = CancellationTokenSource.CreateLinkedTokenSource(ct);

        try
        {
            await SpeakWithMauiTtsAsync(text, languageCode, _ttsCts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TTS cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "SpeakTextAsync failed");
        }
    }

    public Task StopAsync()
    {
        _ttsCts?.Cancel();
        _ttsCts = null;
        return Task.CompletedTask;
    }

    private async Task SpeakWithMauiTtsAsync(string text, string languageCode, CancellationToken ct)
    {
        _logger.LogInformation("MAUI TTS: lang={Lang}, chars={N}", languageCode, text.Length);
        System.Diagnostics.Debug.WriteLine($"[TTS] Speaking ({languageCode}): {text.Substring(0, Math.Min(80, text.Length))}");

        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            try
            {
                var locales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();

                Locale? matched = languageCode switch
                {
                    "en" => locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    "ko" => locales.FirstOrDefault(l => l.Language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    _    => locales.FirstOrDefault(l => l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault()
                };

                System.Diagnostics.Debug.WriteLine($"[TTS] Locale: {matched?.Language}-{matched?.Country ?? "default"}");

                if (ct.IsCancellationRequested) return;

                var options = new SpeechOptions { Locale = matched, Volume = 1.0f, Pitch = 1.0f };
                await TextToSpeech.Default.SpeakAsync(text, options, ct);
                System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync completed");
            }
            catch (OperationCanceledException)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Cancelled");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] Error: {ex.Message}");
                _logger.LogError(ex, "TTS speak failed");
                // Final fallback: default locale
                try
                {
                    if (!ct.IsCancellationRequested)
                        await TextToSpeech.Default.SpeakAsync(text, cancelToken: ct);
                }
                catch { /* ignore */ }
            }
        });
    }
}
