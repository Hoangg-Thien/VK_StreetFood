using Android.OS;
using Android.Runtime;
using Microsoft.Extensions.Logging;
using VK.Mobile.Models;
using VK.Mobile.Services;
using AndroidAudioFocus = Android.Media.AudioFocus;
using AndroidAudioFocusRequest = Android.Media.AudioFocusRequest;
using AndroidAudioFocusRequestClass = Android.Media.AudioFocusRequestClass;
using AndroidAudioManager = Android.Media.AudioManager;
using AndroidTTS = Android.Speech.Tts.TextToSpeech;
using AndroidUtteranceProgressListener = Android.Speech.Tts.UtteranceProgressListener;
using LanguageAvailableResult = Android.Speech.Tts.LanguageAvailableResult;
using OperationResult = Android.Speech.Tts.OperationResult;
using QueueMode = Android.Speech.Tts.QueueMode;
using AndroidVoice = Android.Speech.Tts.Voice;

namespace VK.Mobile.Platforms.Android;

/// <summary>
/// Native Android TTS using android.speech.tts.TextToSpeech (API 21+).
/// Engine is created eagerly in the constructor to avoid deadlock when
/// SpeakTextAsync is called from the UI thread.
/// </summary>
public class AndroidTTSService : Java.Lang.Object, AndroidTTS.IOnInitListener, ITTSService
{
    private const string VoicePreferenceKeyPrefix = "TTSVoice";
    private AndroidTTS? _tts;
    private readonly TaskCompletionSource<bool> _readyTcs =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly ILogger<AndroidTTSService> _logger;
    private readonly AndroidAudioManager? _audioManager;
    private readonly AudioFocusListener _audioFocusListener;
    private readonly TtsProgressListener _progressListener;
    private AndroidAudioFocusRequestClass? _audioFocusRequest;
    private bool _hasAudioFocus;

    // Required by MAUI JNI for Java peer reconstruction
    protected AndroidTTSService(IntPtr handle, JniHandleOwnership transfer)
        : base(handle, transfer)
    {
        _logger = null!;
        _audioManager = null;
        _audioFocusListener = new AudioFocusListener(_ => { });
        _progressListener = new TtsProgressListener(() => { });
    }

    public AndroidTTSService(ILogger<AndroidTTSService> logger)
    {
        _logger = logger;
        _audioManager = (AndroidAudioManager?)global::Android.App.Application.Context
            .GetSystemService(global::Android.Content.Context.AudioService);
        _audioFocusListener = new AudioFocusListener(OnAudioFocusChanged);
        _progressListener = new TtsProgressListener(() =>
            MainThread.BeginInvokeOnMainThread(AbandonAudioFocus));

        // Post to main thread - non-blocking, constructor returns immediately
        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _tts = new AndroidTTS(global::Android.App.Application.Context, this);
                _tts.SetOnUtteranceProgressListener(_progressListener);
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

        var normalizedLang = NormalizeLanguage(languageCode);

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

            if (!RequestAudioFocus())
            {
                _logger.LogWarning("[TTS] Audio focus denied, skip speak");
                return;
            }

            Java.Util.Locale locale = normalizedLang switch
            {
                "en" => Java.Util.Locale.English!,
                "ko" => Java.Util.Locale.Korean!,
                _ => new Java.Util.Locale("vi", "VN")
            };

            var langResult = _tts.SetLanguage(locale);
            _logger.LogDebug("[TTS] SetLanguage({Lang}): {Result}", normalizedLang, langResult);

            if (langResult is LanguageAvailableResult.MissingData
                           or LanguageAvailableResult.NotSupported)
            {
                _logger.LogWarning("[TTS] Language unavailable, falling back to Locale.Default");
                _tts.SetLanguage(Java.Util.Locale.Default);
            }

            var preferredVoiceId = GetPreferredVoiceId(normalizedLang);
            if (!string.IsNullOrWhiteSpace(preferredVoiceId))
            {
                var preferredVoiceApplied = TryApplyVoice(preferredVoiceId);
                if (!preferredVoiceApplied)
                    _logger.LogDebug("[TTS] Preferred voice not available: {VoiceId}", preferredVoiceId);
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

    public async Task<IReadOnlyList<TtsVoiceOption>> GetAvailableVoicesAsync(string languageCode, CancellationToken ct = default)
    {
        bool ready;
        try
        {
            ready = await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(8), ct);
        }
        catch
        {
            return Array.Empty<TtsVoiceOption>();
        }

        if (!ready || _tts == null)
            return Array.Empty<TtsVoiceOption>();

        try
        {
            return await MainThread.InvokeOnMainThreadAsync(() =>
            {
                if (_tts?.Voices == null)
                    return (IReadOnlyList<TtsVoiceOption>)Array.Empty<TtsVoiceOption>();

                var normalizedLang = NormalizeLanguage(languageCode);
                var voices = _tts.Voices
                    .Where(v => v != null)
                    .Where(v => IsVoiceMatchingLanguage(v!, normalizedLang))
                    .Select(v => new TtsVoiceOption(v!.Name, BuildVoiceDisplayName(v)))
                    .GroupBy(v => v.Id)
                    .Select(g => g.First())
                    .OrderBy(v => v.DisplayName)
                    .ToList();

                return (IReadOnlyList<TtsVoiceOption>)voices;
            });
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[TTS] GetAvailableVoicesAsync failed");
            return Array.Empty<TtsVoiceOption>();
        }
    }

    public async Task<bool> SetPreferredVoiceAsync(string voiceId, string languageCode, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(voiceId))
            return false;

        bool ready;
        try
        {
            ready = await _readyTcs.Task.WaitAsync(TimeSpan.FromSeconds(8), ct);
        }
        catch
        {
            return false;
        }

        if (!ready || _tts == null)
            return false;

        try
        {
            var applied = await MainThread.InvokeOnMainThreadAsync(() => TryApplyVoice(voiceId));
            if (!applied)
                return false;

            Preferences.Set(GetVoicePreferenceKey(languageCode), voiceId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[TTS] SetPreferredVoiceAsync failed");
            return false;
        }
    }

    public string? GetPreferredVoiceId(string languageCode)
    {
        var key = GetVoicePreferenceKey(languageCode);
        var value = Preferences.Get(key, string.Empty);
        return string.IsNullOrWhiteSpace(value) ? null : value;
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
        MainThread.BeginInvokeOnMainThread(() =>
        {
            _tts?.Stop();
            AbandonAudioFocus();
        });
        return Task.CompletedTask;
    }

    private bool RequestAudioFocus()
    {
        if (_audioManager == null) return true;

        try
        {
            AndroidAudioFocusRequest result;

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                _audioFocusRequest ??= new AndroidAudioFocusRequestClass
                    .Builder(AndroidAudioFocus.GainTransientMayDuck)
                    .SetOnAudioFocusChangeListener(_audioFocusListener)
                    .Build();

                result = _audioManager.RequestAudioFocus(_audioFocusRequest);
            }
            else
            {
#pragma warning disable CA1422
                result = _audioManager.RequestAudioFocus(
                    _audioFocusListener,
                    global::Android.Media.Stream.Music,
                    AndroidAudioFocus.GainTransientMayDuck);
#pragma warning restore CA1422
            }

            _hasAudioFocus = result == AndroidAudioFocusRequest.Granted;
            return _hasAudioFocus;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[TTS] RequestAudioFocus failed, continue without focus guard");
            return true;
        }
    }

    private void AbandonAudioFocus()
    {
        if (!_hasAudioFocus || _audioManager == null) return;

        try
        {
            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                if (_audioFocusRequest != null)
                    _audioManager.AbandonAudioFocusRequest(_audioFocusRequest);
            }
            else
            {
#pragma warning disable CA1422
                _audioManager.AbandonAudioFocus(_audioFocusListener);
#pragma warning restore CA1422
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "[TTS] AbandonAudioFocus failed");
        }
        finally
        {
            _hasAudioFocus = false;
        }
    }

    private void OnAudioFocusChanged(AndroidAudioFocus focusChange)
    {
        if (focusChange is AndroidAudioFocus.Loss
            or AndroidAudioFocus.LossTransient
            or AndroidAudioFocus.LossTransientCanDuck)
        {
            MainThread.BeginInvokeOnMainThread(() =>
            {
                _logger.LogInformation("[TTS] Focus lost ({Focus}), stop narration", focusChange);
                _tts?.Stop();
                AbandonAudioFocus();
            });
        }
    }

    private bool TryApplyVoice(string voiceId)
    {
        if (_tts?.Voices == null)
            return false;

        var targetVoice = _tts.Voices
            .FirstOrDefault(v => v != null && string.Equals(v.Name, voiceId, StringComparison.Ordinal));

        if (targetVoice == null)
            return false;

        var result = _tts.SetVoice(targetVoice);
        return result == OperationResult.Success;
    }

    private static bool IsVoiceMatchingLanguage(AndroidVoice voice, string normalizedLang)
    {
        var locale = voice.Locale;
        if (locale == null)
            return false;

        var voiceLanguage = (locale.Language ?? string.Empty).ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(voiceLanguage))
            return false;

        return voiceLanguage == normalizedLang;
    }

    private static string BuildVoiceDisplayName(AndroidVoice voice)
    {
        var locale = voice.Locale;
        var localeText = locale == null
            ? string.Empty
            : $" ({locale.DisplayLanguage}-{locale.Country})";

        return string.IsNullOrWhiteSpace(localeText)
            ? voice.Name
            : $"{voice.Name}{localeText}";
    }

    private static string NormalizeLanguage(string languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return "vi";

        var code = languageCode.Trim().ToLowerInvariant();
        var separatorIndex = code.IndexOfAny(new[] { '-', '_' });
        return separatorIndex > 0 ? code[..separatorIndex] : code;
    }

    private static string GetVoicePreferenceKey(string languageCode)
        => $"{VoicePreferenceKeyPrefix}.{NormalizeLanguage(languageCode)}";

    private sealed class AudioFocusListener(Action<AndroidAudioFocus> onFocusChanged)
        : Java.Lang.Object, AndroidAudioManager.IOnAudioFocusChangeListener
    {
        private readonly Action<AndroidAudioFocus> _onFocusChanged = onFocusChanged;
        public void OnAudioFocusChange(AndroidAudioFocus focusChange) => _onFocusChanged(focusChange);
    }

    private sealed class TtsProgressListener(Action onCompleted)
        : AndroidUtteranceProgressListener
    {
        private readonly Action _onCompleted = onCompleted;
        public override void OnStart(string? utteranceId) { }
        public override void OnDone(string? utteranceId) => _onCompleted();
        [Obsolete]
        public override void OnError(string? utteranceId) => _onCompleted();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _tts?.Stop();
            _tts?.Shutdown();
            _tts = null;
            AbandonAudioFocus();
        }
        base.Dispose(disposing);
    }
}
