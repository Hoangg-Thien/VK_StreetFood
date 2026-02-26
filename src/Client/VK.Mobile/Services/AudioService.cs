using Plugin.Maui.Audio;
using VK.Mobile.Models;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.Services;

public interface IAudioService
{
    /// <summary>
    /// Thêm audio vào queue và phát ngay nếu queue trống.
    /// Nếu cùng POI đang phát, bỏ qua (không phát trùng lặp).
    /// </summary>
    Task<bool> PlayAudioAsync(string audioUrl, int? poiId = null, int priority = 0);

    /// <summary>Dừng hẳn và xóa toàn bộ queue.</summary>
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();

    bool IsPlaying { get; }
    double CurrentPosition { get; }
    double Duration { get; }
    string? CurrentUrl { get; }
    int? CurrentPOIId { get; }

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
            _currentPlayer.PlaybackEnded += (s, e) =>
            {
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
                tcs.TrySetResult(true);
            };

            _currentPlayer.Play();
            await tcs.Task;
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
            // Cache theo URL để không download lại
            var safeHash = Math.Abs(audioUrl.GetHashCode()).ToString();
            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"audio_{safeHash}.mp3");

            if (!File.Exists(tempPath))
            {
                var bytes = await _httpClient.GetByteArrayAsync(audioUrl);
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

    // ─── Audio Focus ──────────────────────────────────────────────────────────

    private static void RequestAudioFocus()
    {
#if ANDROID
        try
        {
            var am = (global::Android.Media.AudioManager?)
                global::Android.App.Application.Context.GetSystemService(
                    global::Android.Content.Context.AudioService);

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                var req = new global::Android.Media.AudioFocusRequestClass
                    .Builder(global::Android.Media.AudioFocus.GainTransient).Build();
                am?.RequestAudioFocus(req);
            }
            else
            {
#pragma warning disable CA1422
                am?.RequestAudioFocus(null,
                    global::Android.Media.Stream.Music,
                    global::Android.Media.AudioFocus.GainTransient);
#pragma warning restore CA1422
            }
        }
        catch { /* best effort */ }
#endif
#if IOS
        try { AVFoundation.AVAudioSession.SharedInstance().SetActive(true, out _); }
        catch { /* best effort */ }
#endif
    }

    private static void AbandonAudioFocus()
    {
#if ANDROID
        try
        {
            var am = (global::Android.Media.AudioManager?)
                global::Android.App.Application.Context.GetSystemService(
                    global::Android.Content.Context.AudioService);

            if (global::Android.OS.Build.VERSION.SdkInt >= global::Android.OS.BuildVersionCodes.O)
            {
                var req = new global::Android.Media.AudioFocusRequestClass
                    .Builder(global::Android.Media.AudioFocus.GainTransient).Build();
                am?.AbandonAudioFocusRequest(req);
            }
            else
            {
#pragma warning disable CA1422
                am?.AbandonAudioFocus(null);
#pragma warning restore CA1422
            }
        }
        catch { /* best effort */ }
#endif
#if IOS
        try { AVFoundation.AVAudioSession.SharedInstance().SetActive(false, out _); }
        catch { /* best effort */ }
#endif
    }
}
