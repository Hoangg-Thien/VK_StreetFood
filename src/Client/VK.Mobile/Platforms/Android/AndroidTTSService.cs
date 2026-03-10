using Microsoft.Extensions.Logging;
using VK.Mobile.Models;
using VK.Mobile.Services;
using AndroidTTS = Android.Speech.Tts.TextToSpeech;
using QueueMode = Android.Speech.Tts.QueueMode;
using OperationResult = Android.Speech.Tts.OperationResult;
using LanguageAvailableResult = Android.Speech.Tts.LanguageAvailableResult;

namespace VK.Mobile.Platforms.Android;

public class AndroidTTSService : Java.Lang.Object, AndroidTTS.IOnInitListener, ITTSService
{
    private AndroidTTS? _tts;
    private TaskCompletionSource<bool>? _initTcs;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private readonly ILogger<AndroidTTSService> _logger;
    private bool _ready;

    public AndroidTTSService(ILogger<AndroidTTSService> logger)
    {
        _logger = logger;
    }

    // Android calls this on main thread when TTS engine is ready
    void AndroidTTS.IOnInitListener.OnInit(OperationResult status)
    {
        _ready = status == OperationResult.Success;
        System.Diagnostics.Debug.WriteLine($"[TTS] OnInit: {status} ready={_ready}");
        _initTcs?.TrySetResult(_ready);
    }

    // Thread-safe lazy init  creates AndroidTTS on main thread, waits for OnInit callback
    private async Task<bool> EnsureReadyAsync()
    {
        if (_ready && _tts != null) return true;

        await _initLock.WaitAsync();
        try
        {
            if (_ready && _tts != null) return true;

            _initTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            // new AndroidTTS() must be called on the main thread
            await MainThread.InvokeOnMainThreadAsync(() =>
            {
                _tts = new AndroidTTS(global::Android.App.Application.Context, this);
            });

            System.Diagnostics.Debug.WriteLine("[TTS] AndroidTTS created, waiting for OnInit...");

            // Wait up to 5 s for OnInit callback
            try
            {
                return await _initTcs.Task.WaitAsync(TimeSpan.FromSeconds(5));
            }
            catch (TimeoutException)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] OnInit timed out!");
                return false;
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[TTS] EnsureReady error: {ex.Message}");
            _logger.LogError(ex, "[TTS] Init failed");
            return false;
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        System.Diagnostics.Debug.WriteLine($"[TTS] SpeakTextAsync: lang={languageCode} len={text.Length}");

        var ready = await EnsureReadyAsync();
        System.Diagnostics.Debug.WriteLine($"[TTS] Ready={ready}");

        if (!ready || _tts == null || ct.IsCancellationRequested) return;

        // SetLanguage + Speak must run on main thread
        await MainThread.InvokeOnMainThreadAsync(() =>
        {
            if (ct.IsCancellationRequested) return;

            Java.Util.Locale locale = languageCode switch
            {
                "en" => Java.Util.Locale.English!,
                "ko" => Java.Util.Locale.Korean!,
                _    => new Java.Util.Locale("vi", "VN")
            };

            var langResult = _tts.SetLanguage(locale);
            System.Diagnostics.Debug.WriteLine($"[TTS] SetLanguage({languageCode}): {langResult}");

            // Fallback to device default if language not available
            if (langResult == LanguageAvailableResult.MissingData
             || langResult == LanguageAvailableResult.NotSupported)
            {
                System.Diagnostics.Debug.WriteLine("[TTS] Fallback to device default locale");
                _tts.SetLanguage(Java.Util.Locale.Default);
            }

            // QueueMode.Flush clears queue and speaks immediately
            var r = _tts.Speak(text, QueueMode.Flush, null, null);
            System.Diagnostics.Debug.WriteLine($"[TTS] Speak() => {r}");
        });
    }

    public async Task SpeakPOIAsync(POIModel poi, string languageCode, CancellationToken ct = default)
    {
        var text = string.IsNullOrWhiteSpace(poi.Description)
            ? poi.Name
            : $"{poi.Name}. {poi.Description}";
        await SpeakTextAsync(text, languageCode, ct);
    }

    public Task StopAsync()
    {
        MainThread.BeginInvokeOnMainThread(() => _tts?.Stop());
        return Task.CompletedTask;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tts?.Stop();
            _tts?.Shutdown();
            _tts = null;
            _initLock.Dispose();
        }
        base.Dispose(disposing);
    }
}
