using Microsoft.Extensions.Logging;
using VK.Mobile.Models;

namespace VK.Mobile.Services;

public interface ITTSService
{
    Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default);
    Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default);
    Task StopAsync();
    Task<IReadOnlyList<TtsVoiceOption>> GetAvailableVoicesAsync(string languageCode, CancellationToken ct = default);
    Task<bool> SetPreferredVoiceAsync(string voiceId, string languageCode, CancellationToken ct = default);
    string? GetPreferredVoiceId(string languageCode);
}

public sealed record TtsVoiceOption(string Id, string DisplayName);

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

        _cts.Cancel();
        _cts.Dispose();
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var token = _cts.Token;

        if (token.IsCancellationRequested) return;

        System.Diagnostics.Debug.WriteLine($"[TTS] SpeakTextAsync called: lang={languageCode}, textLen={text.Length}, text={text[..Math.Min(50, text.Length)]}");

        try
        {
            // Speak without specifying locale — uses device's TTS language setting
            // (avoids silent failures when locale code doesn't exactly match installed voice)
            await TextToSpeech.Default.SpeakAsync(text, new SpeechOptions
            {
                Volume = 1.0f,
                Pitch = 1.0f
            }, token);

            System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync completed successfully");
        }
        catch (OperationCanceledException)
        {
            System.Diagnostics.Debug.WriteLine("[TTS] SpeakAsync cancelled");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] SpeakAsync exception: {ex.GetType().Name} - {ex.Message}");
            _logger.LogError(ex, "[TTS] SpeakAsync failed");
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

    public Task<IReadOnlyList<TtsVoiceOption>> GetAvailableVoicesAsync(string languageCode, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<TtsVoiceOption>>(Array.Empty<TtsVoiceOption>());

    public Task<bool> SetPreferredVoiceAsync(string voiceId, string languageCode, CancellationToken ct = default)
        => Task.FromResult(false);

    public string? GetPreferredVoiceId(string languageCode)
        => null;
}
