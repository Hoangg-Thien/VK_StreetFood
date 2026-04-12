using Plugin.Maui.Audio;
using VK.Mobile.Models;
using Microsoft.Extensions.Logging;
using System.Security.Cryptography;
using System.Text;

namespace VK.Mobile.Services;

public interface IAudioService
{
    /// <summary>
    /// Thêm audio vào queue và phát ngay nếu queue trống.
    /// Nếu cùng POI đang phát, bỏ qua (không phát trùng lặp).
    /// </summary>
    Task<bool> PlayAudioAsync(string audioUrl, int? poiId = null, int priority = 0);

    /// <summary>
    /// Tải sẵn audio vào cache để phát nhanh hơn khi người dùng bấm nghe.
    /// </summary>
    Task<bool> PreloadAudioAsync(string audioUrl);

    /// <summary>Dừng hẳn và xóa toàn bộ queue.</summary>
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();

    bool IsPlaying { get; }
    double CurrentPosition { get; }
    double Duration { get; }
    string? CurrentUrl { get; }
    int? CurrentPOIId { get; }

    Task SeekAsync(double positionSeconds);

    event EventHandler? PlaybackCompleted;
    event EventHandler<string>? PlaybackError;
}

// ─────────────────────────────────────────────────────────────────────────────
internal record AudioQueueItem(string Url, int? POIId, int Priority);

// ─────────────────────────────────────────────────────────────────────────────
public class AudioService : IAudioService
{
    private readonly IAudioManager _audioManager;
    private readonly ILogger<AudioService> _logger;
    private readonly HttpClient _httpClient;

    private IAudioPlayer? _currentPlayer;
    private readonly Queue<AudioQueueItem> _queue = new();
    private readonly SemaphoreSlim _lock = new(1, 1);
    private bool _isProcessingQueue;
    private TaskCompletionSource<bool>? _currentPlayTcs;

#if ANDROID
    private global::Android.Media.AudioManager? _androidAudioManager;
    private AndroidAudioFocusListener? _androidAudioFocusListener;
    private global::Android.Media.AudioFocusRequestClass? _androidAudioFocusRequest;
    private bool _hasAndroidAudioFocus;
#endif

    public event EventHandler? PlaybackCompleted;
    public event EventHandler<string>? PlaybackError;

    public bool IsPlaying => _currentPlayer?.IsPlaying ?? false;
    public double CurrentPosition => _currentPlayer?.CurrentPosition ?? 0;
    public double Duration => _currentPlayer?.Duration ?? 0;
    public string? CurrentUrl { get; private set; }
    public int? CurrentPOIId { get; private set; }

    public AudioService(IAudioManager audioManager, ILogger<AudioService> logger)
    {
        _audioManager = audioManager;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(AppSettings.AudioBaseUrl);

#if ANDROID
        _androidAudioManager = (global::Android.Media.AudioManager?)
            global::Android.App.Application.Context.GetSystemService(
                global::Android.Content.Context.AudioService);
        _androidAudioFocusListener = new AndroidAudioFocusListener(OnAndroidAudioFocusChanged);
#endif
    }

    public async Task<bool> PlayAudioAsync(string audioUrl, int? poiId = null, int priority = 0)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
            return false;

        await _lock.WaitAsync();
        try
        {
            // ── Không phát trùng lặp: cùng URL đang phát → bỏ qua ─────────
            if (IsPlaying && CurrentUrl == audioUrl)
            {
                _logger.LogDebug("Audio already playing for same URL, skipping duplicate");
                return true;
            }

            // ── Cùng POI đang phát → bỏ qua ───────────────────────────────
            if (IsPlaying && poiId.HasValue && CurrentPOIId == poiId)
            {
                _logger.LogDebug("Audio already playing for POI {Id}, skipping", poiId);
                return true;
            }

            _queue.Enqueue(new AudioQueueItem(audioUrl, poiId, priority));
            _logger.LogInformation("Queued audio for POI {Id}, queue size: {Size}", poiId, _queue.Count);
        }
        finally
        {
            _lock.Release();
        }

        if (!_isProcessingQueue)
            _ = ProcessQueueAsync();

        return true;
    }

    public async Task<bool> PreloadAudioAsync(string audioUrl)
    {
        if (string.IsNullOrWhiteSpace(audioUrl))
            return false;

        var tempPath = await DownloadToTempAsync(audioUrl);
        return !string.IsNullOrWhiteSpace(tempPath);
    }

    private async Task ProcessQueueAsync()
    {
        if (_isProcessingQueue) return;
        _isProcessingQueue = true;

        try
        {
            while (true)
            {
                AudioQueueItem? item = null;

                await _lock.WaitAsync();
                try
                {
                    if (_queue.Count == 0) break;
                    item = _queue.Dequeue();
                }
                finally
                {
                    _lock.Release();
                }

                if (item == null) break;
                await PlayItemAsync(item);
            }
        }
        finally
        {
            _isProcessingQueue = false;
        }
    }

    private async Task PlayItemAsync(AudioQueueItem item)
    {
        try
        {
            await StopCurrentPlayerAsync();

            _logger.LogInformation("Playing audio: {Url} (POI {Id})", item.Url, item.POIId);

            var tempPath = await DownloadToTempAsync(item.Url);
            if (tempPath == null)
            {
                PlaybackError?.Invoke(this, $"Failed to download: {item.Url}");
                return;
            }

            RequestAudioFocus();

            var stream = File.OpenRead(tempPath);
            _currentPlayer = _audioManager.CreatePlayer(stream);
            CurrentUrl = item.Url;
            CurrentPOIId = item.POIId;

            var tcs = new TaskCompletionSource<bool>();
            _currentPlayTcs = tcs;
            _currentPlayer.PlaybackEnded += (s, e) =>
            {
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                tcs.TrySetResult(true);
            };

            _currentPlayer.Play();
            await tcs.Task;
            _currentPlayTcs = null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio: {Url}", item.Url);
            PlaybackError?.Invoke(this, ex.Message);
        }
        finally
        {
            CurrentUrl = null;
            CurrentPOIId = null;
            AbandonAudioFocus();
        }
    }

    private async Task<string?> DownloadToTempAsync(string audioUrl)
    {
        try
        {
            var absoluteUrl = ToAbsoluteUrl(audioUrl);

            // Cache theo SHA256 URL để tránh collision từ GetHashCode.
            var safeHash = ComputeStableHash(absoluteUrl);
            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"audio_{safeHash}.mp3");

            // Nếu cache file rỗng/hỏng thì buộc tải lại.
            if (File.Exists(tempPath))
            {
                var length = new FileInfo(tempPath).Length;
                if (length <= 1024)
                {
                    _logger.LogWarning("Invalid cached audio (size={Size}) at {Path}, re-downloading", length, tempPath);
                    File.Delete(tempPath);
                }
            }

            if (!File.Exists(tempPath))
            {
                var bytes = await _httpClient.GetByteArrayAsync(absoluteUrl);
                if (bytes.Length <= 1024)
                {
                    _logger.LogWarning("Downloaded audio too small ({Size} bytes): {Url}", bytes.Length, absoluteUrl);
                    return null;
                }

                await File.WriteAllBytesAsync(tempPath, bytes);
            }
            else
            {
                _logger.LogDebug("Audio cache hit: {Hash}", safeHash);
            }

            return tempPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download audio: {Url}", audioUrl);
            return null;
        }
    }

    private static string ToAbsoluteUrl(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var absolute))
        {
            if (absolute.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                && Uri.TryCreate(AppSettings.AudioBaseUrl, UriKind.Absolute, out var configuredBase)
                && configuredBase.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
                && absolute.Host.Equals(configuredBase.Host, StringComparison.OrdinalIgnoreCase))
            {
                var secureUri = new UriBuilder(absolute)
                {
                    Scheme = Uri.UriSchemeHttps,
                    Port = -1
                };
                return secureUri.Uri.ToString();
            }

            return absolute.ToString();
        }

        var baseUrl = AppSettings.AudioBaseUrl.TrimEnd('/');
        if (url.StartsWith('/'))
            return baseUrl + url;

        return $"{baseUrl}/{url}";
    }

    private static string ComputeStableHash(string input)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(input);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task StopAsync()
    {
        await _lock.WaitAsync();
        try { _queue.Clear(); }
        finally { _lock.Release(); }

        await StopCurrentPlayerAsync();
        AbandonAudioFocus();
    }

    private Task StopCurrentPlayerAsync()
    {
        try
        {
            // Unblock any awaiting tcs in PlayItemAsync before stopping the player
            _currentPlayTcs?.TrySetResult(false);
            _currentPlayTcs = null;

            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.Dispose();
                _currentPlayer = null;
                CurrentUrl = null;
                CurrentPOIId = null;
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error stopping player"); }
        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        try { _currentPlayer?.Pause(); }
        catch (Exception ex) { _logger.LogError(ex, "Error pausing"); }
        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        try { _currentPlayer?.Play(); }
        catch (Exception ex) { _logger.LogError(ex, "Error resuming"); }
        return Task.CompletedTask;
    }

    public Task SeekAsync(double positionSeconds)
    {
        try { _currentPlayer?.Seek(positionSeconds); }
        catch (Exception ex) { _logger.LogError(ex, "Error seeking"); }
        return Task.CompletedTask;
    }

    // ─── Audio Focus ──────────────────────────────────────────────────────────

    private void RequestAudioFocus()
    {
#if ANDROID
        try
        {
            if (_androidAudioManager == null)
                return;

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                _androidAudioFocusRequest ??= new global::Android.Media.AudioFocusRequestClass
                    .Builder(global::Android.Media.AudioFocus.GainTransientMayDuck)
                    .SetOnAudioFocusChangeListener(_androidAudioFocusListener)
                    .Build();

                var result = _androidAudioManager.RequestAudioFocus(_androidAudioFocusRequest);
                _hasAndroidAudioFocus = result == global::Android.Media.AudioFocusRequest.Granted;
            }
            else
            {
#pragma warning disable CA1422
                var result = _androidAudioManager.RequestAudioFocus(
                    _androidAudioFocusListener,
                    global::Android.Media.Stream.Music,
                    global::Android.Media.AudioFocus.GainTransientMayDuck);
#pragma warning restore CA1422

                _hasAndroidAudioFocus = result == global::Android.Media.AudioFocusRequest.Granted;
            }

            if (!_hasAndroidAudioFocus)
                _logger.LogDebug("Audio focus not granted for MP3 playback");
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "RequestAudioFocus failed");
        }
#endif
#if IOS
        try { AVFoundation.AVAudioSession.SharedInstance().SetActive(true, out _); }
        catch { /* best effort */ }
#endif
    }

    private void AbandonAudioFocus()
    {
#if ANDROID
        try
        {
            if (!_hasAndroidAudioFocus || _androidAudioManager == null)
                return;

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                if (_androidAudioFocusRequest != null)
                    _androidAudioManager.AbandonAudioFocusRequest(_androidAudioFocusRequest);
            }
            else
            {
#pragma warning disable CA1422
                _androidAudioManager.AbandonAudioFocus(_androidAudioFocusListener);
#pragma warning restore CA1422
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "AbandonAudioFocus failed");
        }
        finally
        {
            _hasAndroidAudioFocus = false;
        }
#endif
#if IOS
        try { AVFoundation.AVAudioSession.SharedInstance().SetActive(false, out _); }
        catch { /* best effort */ }
#endif
    }

#if ANDROID
    private void OnAndroidAudioFocusChanged(global::Android.Media.AudioFocus focusChange)
    {
        if (focusChange is not (
            global::Android.Media.AudioFocus.Loss or
            global::Android.Media.AudioFocus.LossTransient or
            global::Android.Media.AudioFocus.LossTransientCanDuck))
        {
            return;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                _logger.LogInformation("Audio focus lost ({Focus}), stopping current MP3 playback", focusChange);
                _currentPlayer?.Stop();
                _currentPlayTcs?.TrySetResult(false);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Error handling audio focus loss");
            }
        });
    }

    private sealed class AndroidAudioFocusListener(Action<global::Android.Media.AudioFocus> onFocusChanged)
        : Java.Lang.Object, global::Android.Media.AudioManager.IOnAudioFocusChangeListener
    {
        private readonly Action<global::Android.Media.AudioFocus> _onFocusChanged = onFocusChanged;
        public void OnAudioFocusChange(global::Android.Media.AudioFocus focusChange)
            => _onFocusChanged(focusChange);
    }
#endif
}
