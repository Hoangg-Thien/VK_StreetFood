using Android.OS;
using Android.Runtime;
using Microsoft.Extensions.Logging;
using VK.Mobile.Models;
using VK.Mobile.Services;
using AndroidTTS = Android.Speech.Tts.TextToSpeech;
using LanguageAvailableResult = Android.Speech.Tts.LanguageAvailableResult;
using OperationResult = Android.Speech.Tts.OperationResult;
using QueueMode = Android.Speech.Tts.QueueMode;

namespace VK.Mobile.Platforms.Android;

/// <summary>
/// Native Android TTS using android.speech.tts.TextToSpeech (API 21+).
/// Engine is created eagerly in the constructor to avoid deadlock when
/// SpeakTextAsync is called from the UI thread.
/// </summary>
public class AndroidTTSService : Java.Lang.Object, AndroidTTS.IOnInitListener, ITTSService
{
    private AndroidTTS? _tts;
    private readonly TaskCompletionSource<bool> _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogger<AndroidTTSService> _logger;

    // Required by MAUI JNI for Java peer reconstruction
    protected AndroidTTSService(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer) { _logger = null!; }

    public AndroidTTSService(ILogger<AndroidTTSService> logger)
    {
        _logger = logger;
        // Post to main thread - non-blocking, constructor returns immediately
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _tts = new AndroidTTS(global::Android.App.Application.Context, this);
                _logger.LogDebug("[TTS] Engine created, awaiting OnInit...");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TTS] Failed to create engine");
                _readyTcs.TrySetResult(false);
            }
        });
    }

    // Called by Android on main thread once TTS service is bound
    void AndroidTTS.IOnInitListener.OnInit(OperationResult status)
    {
        var ok = status == OperationResult.Success;
        _logger.LogDebug("[TTS] OnInit: {Status} ready={Ok}", status, ok);
        _readyTcs.TrySetResult(ok);
    }

    public async Task SpeakTextAsync(string text, string languageCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        _logger.LogDebug("[TTS] SpeakTextAsync lang={Lang} chars={Len}", languageCode, text.Length);

        // Yields the calling thread so OnInit can fire on the main thread
        bool ready;
        try
        {
            ready = await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(8), ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TTS] Engine ready wait failed");
            return;
        }

        if (!ready || _tts == null || ct.IsCancellationRequested)
        {
            _logger.LogWarning("[TTS] Aborting: ready={R} hasTts={T} cancelled={C}",
                ready, _tts != null, ct.IsCancellationRequested);
            return;
        }

        // Android TTS API must run on the main thread
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
            _logger.LogDebug("[TTS] SetLanguage({Lang}): {Result}", languageCode, langResult);

            if (langResult is LanguageAvailableResult.MissingData
                           or LanguageAvailableResult.NotSupported)
            {
                _logger.LogWarning("[TTS] Language unavailable, falling back to Locale.Default");
                _tts.SetLanguage(Java.Util.Locale.Default);
            }

            _tts.SetSpeechRate(1.0f);
            _tts.SetPitch(1.0f);

            // Route to STREAM_MUSIC so audio follows media volume, not ring/notification
            var bundle = new Bundle();
            bundle.PutInt("streamType", (int)global::Android.Media.Stream.Music);
            bundle.PutFloat("volume", 1.0f);

            var result = _tts.Speak(text, QueueMode.Flush, bundle, "vk_utterance");
            _logger.LogDebug("[TTS] Speak() => {Result}", result);
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
        }
        base.Dispose(disposing);
    }
}
