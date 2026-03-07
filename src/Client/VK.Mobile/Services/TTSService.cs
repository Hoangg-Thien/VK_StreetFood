using Microsoft.Extensions.Logging;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITTSService
{
    Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default);
    Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default);
    Task StopAsync();
}

public class TTSService : ITTSService
{
    private readonly ILogger<TTSService> _logger;
    private CancellationTokenSource _cts = new();

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

        // Cancel previous
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();

        System.Diagnostics.Debug.WriteLine($"[TTS] SpeakTextAsync: lang={languageCode} len={text.Length}");
        _logger.LogInformation("[TTS] Speaking lang={Lang} chars={N}", languageCode, text.Length);

        try
        {
            // Resolve locale off-main-thread (just a list search)
            Locale? locale = null;
            try
            {
                var locales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();
                System.Diagnostics.Debug.WriteLine($"[TTS] Locales available: {locales.Count}");

                locale = languageCode switch
                {
                    "en" => locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    "ko" => locales.FirstOrDefault(l => l.Language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    _    => locales.FirstOrDefault(l => l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault()
                };
                System.Diagnostics.Debug.WriteLine($"[TTS] Chose locale: {locale?.Language ?? "null"}-{locale?.Country ?? "null"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] GetLocalesAsync failed: {ex.Message}");
            }

            var options = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f, Locale = locale };

            // Dispatcher.DispatchAsync properly awaits Func<Task>  no ambiguity with Action overload
            await Application.Current!.Dispatcher.DispatchAsync(async () =>
            {
                System.Diagnostics.Debug.WriteLine("[TTS] On dispatcher  calling SpeakAsync...");
                try
                {
                    await TextToSpeech.Default.SpeakAsync(text, options);
                    System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync completed OK");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] SpeakAsync error: {ex.GetType().Name}: {ex.Message}");
                    _logger.LogWarning(ex, "[TTS] SpeakAsync failed, retry without locale");
                    try
                    {
                        // Retry: no locale, no options at all
                        await TextToSpeech.Default.SpeakAsync(text);
                        System.Diagnostics.Debug.WriteLine("[TTS] Fallback SpeakAsync (no locale) OK");
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TTS] Fallback also failed: {ex2.Message}");
                    }
                }
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Outer error: {ex.Message}");
            _logger.LogError(ex, "[TTS] SpeakTextAsync outer error");
        }
    }

    public Task StopAsync()
    {
        System.Diagnostics.Debug.WriteLine("[TTS] StopAsync");
        _cts.Cancel();
        return Task.CompletedTask;
    }
}
