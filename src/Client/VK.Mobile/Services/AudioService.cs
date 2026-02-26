using Plugin.Maui.Audio;
using VK.Mobile.Models;
using Microsoft.Extensions.Logging;

namespace VK.Mobile.Services;

public interface IAudioService
{
    Task<bool> PlayAudioAsync(string audioUrl);
    Task StopAsync();
    Task PauseAsync();
    Task ResumeAsync();
    bool IsPlaying { get; }
    double CurrentPosition { get; }
    double Duration { get; }
    event EventHandler? PlaybackCompleted;
}

public class AudioService : IAudioService
{
    private readonly IAudioManager _audioManager;
    private readonly ILogger<AudioService> _logger;
    private IAudioPlayer? _currentPlayer;
    private readonly HttpClient _httpClient;

    public event EventHandler? PlaybackCompleted;

    public bool IsPlaying => _currentPlayer?.IsPlaying ?? false;
    public double CurrentPosition => _currentPlayer?.CurrentPosition ?? 0;
    public double Duration => _currentPlayer?.Duration ?? 0;

    public AudioService(IAudioManager audioManager, ILogger<AudioService> logger)
    {
        _audioManager = audioManager;
        _logger = logger;
        _httpClient = new HttpClient();
        _httpClient.BaseAddress = new Uri(AppSettings.AudioBaseUrl);
    }

    public async Task<bool> PlayAudioAsync(string audioUrl)
    {
        try
        {
            await StopAsync();

            _logger.LogInformation("Playing audio: {Url}", audioUrl);

            // Download audio file to temp
            var audioBytes = await _httpClient.GetByteArrayAsync(audioUrl);

            // Save to temp file
            var tempPath = Path.Combine(FileSystem.CacheDirectory, $"temp_audio_{Guid.NewGuid()}.mp3");
            await File.WriteAllBytesAsync(tempPath, audioBytes);

            // Play from file
            _currentPlayer = _audioManager.CreatePlayer(await FileSystem.OpenAppPackageFileAsync(tempPath));

            _currentPlayer.PlaybackEnded += (s, e) =>
            {
                PlaybackCompleted?.Invoke(this, EventArgs.Empty);
            };

            _currentPlayer.Play();

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error playing audio");
            return false;
        }
    }

    public Task StopAsync()
    {
        try
        {
            if (_currentPlayer != null)
            {
                _currentPlayer.Stop();
                _currentPlayer.Dispose();
                _currentPlayer = null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error stopping audio");
        }

        return Task.CompletedTask;
    }

    public Task PauseAsync()
    {
        try
        {
            _currentPlayer?.Pause();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error pausing audio");
        }

        return Task.CompletedTask;
    }

    public Task ResumeAsync()
    {
        try
        {
            _currentPlayer?.Play();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error resuming audio");
        }

        return Task.CompletedTask;
    }
}
