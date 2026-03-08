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

        // Stop any current speech and create new token
        _cts.Cancel();
        _cts.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        System.Diagnostics.Debug.WriteLine($"[TTS] SpeakTextAsync: lang={languageCode} len={text.Length}");
        _logger.LogInformation("[TTS] Speaking lang={Lang} chars={N}", languageCode, text.Length);

        try
        {
            // Resolve locale (can run off main thread)
            Locale? locale = null;
            try
            {
                var locales = (await TextToSpeech.Default.GetLocalesAsync()).ToList();
                System.Diagnostics.Debug.WriteLine($"[TTS] Locales: {locales.Count}");
                locale = languageCode switch
                {
                    "en" => locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    "ko" => locales.FirstOrDefault(l => l.Language.StartsWith("ko", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase)),
                    _    => locales.FirstOrDefault(l => l.Language.StartsWith("vi", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault(l => l.Language.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                             ?? locales.FirstOrDefault()
                };
                System.Diagnostics.Debug.WriteLine($"[TTS] Locale: {locale?.Language ?? "null"}-{locale?.Country ?? "null"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TTS] GetLocalesAsync failed: {ex.Message}");
            }

            if (token.IsCancellationRequested) return;

            var options = new SpeechOptions { Volume = 1.0f, Pitch = 1.0f, Locale = locale };

            // Use Dispatcher.DispatchAsync (Func<Task> overload, unambiguous vs Action overload)
            await Application.Current!.Dispatcher.DispatchAsync(async () =>
            {
                if (token.IsCancellationRequested) return;
                System.Diagnostics.Debug.WriteLine("[TTS] Calling SpeakAsync...");
                try
                {
                    // Pass token so cancellation (Stop) actually interrupts the engine
                    await TextToSpeech.Default.SpeakAsync(text, options, token);
                    System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync completed");
                }
                catch (OperationCanceledException)
                {
                    System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync cancelled (stop requested)");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[TTS] SpeakAsync error: {ex.GetType().Name}: {ex.Message}");
                    _logger.LogWarning(ex, "[TTS] SpeakAsync failed, retry without locale");
                    try
                    {
                        if (!token.IsCancellationRequested)
                            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions { Volume = 1.0f }, token);
                    }
                    catch (Exception ex2)
                    {
                        System.Diagnostics.Debug.WriteLine($"[TTS] Fallback failed: {ex2.Message}");
                    }
                }
            });
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[TTS] Outer cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] Outer error: {ex.Message}");
            _logger.LogError(ex, "[TTS] SpeakTextAsync failed");
        }
    }

    public Task StopAsync()
    {
        System.Diagnostics.Debug.WriteLine("[TTS] StopAsync - cancelling");
        _cts.Cancel();
        _cts.Dispose();
        _cts = new CancellationTokenSource();
        return Task.CompletedTask;
    }
}
