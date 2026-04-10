using System.Diagnostics;
using System.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.API.Services;

public interface ITtsGenerationService
{
    Task<TtsGenerateResult> GenerateAsync(int audioContentId, CancellationToken ct = default);
    Task<List<TtsGenerateResult>> GenerateForPoiAsync(int poiId, CancellationToken ct = default);
    Task<List<TtsGenerateResult>> GenerateAllMissingAsync(CancellationToken ct = default);
}

public record TtsGenerateResult(
    int AudioContentId,
    int PoiId,
    string LanguageCode,
    bool Success,
    string? AudioFileUrl,
    string? Error);

// ─── Voice map (3 ngôn ngữ) ──────────────────────────────────────────────────
file static class EdgeVoices
{
    public static readonly Dictionary<string, string> Map = new()
    {
        ["vi"] = "vi-VN-HoaiMyNeural",
        ["en"] = "en-US-JennyNeural",
        ["ko"] = "ko-KR-SunHiNeural",
    };
}

public class TtsGenerationService : ITtsGenerationService
{
    private readonly IRepository<AudioContent> _audioRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly string _audioRootPath;
    private readonly ILogger<TtsGenerationService> _logger;

    public TtsGenerationService(
        IRepository<AudioContent> audioRepository,
        IUnitOfWork unitOfWork,
        IOptions<AudioStorageOptions> audioStorageOptions,
        ILogger<TtsGenerationService> logger)
    {
        _audioRepository = audioRepository;
        _unitOfWork = unitOfWork;
        _audioRootPath = audioStorageOptions.Value.RootPath;
        _logger = logger;

        if (string.IsNullOrWhiteSpace(_audioRootPath))
            throw new InvalidOperationException("Audio storage root path is not configured.");
    }

    // ─── Public API ───────────────────────────────────────────────────────────

    public async Task<TtsGenerateResult> GenerateAsync(int audioContentId, CancellationToken ct = default)
    {
        var audio = await _audioRepository.Query()
            .FirstOrDefaultAsync(a => a.Id == audioContentId && !a.IsDeleted, ct);

        if (audio == null)
            return new TtsGenerateResult(audioContentId, 0, "", false, null, "AudioContent not found");

        return await GenerateForAudioAsync(audio, ct);
    }

    public async Task<List<TtsGenerateResult>> GenerateForPoiAsync(int poiId, CancellationToken ct = default)
    {
        var list = await _audioRepository.Query()
            .Where(a => a.PointOfInterestId == poiId && !a.IsDeleted
                        && EdgeVoices.Map.Keys.Contains(a.LanguageCode))
            .ToListAsync(ct);

        var results = new List<TtsGenerateResult>();
        foreach (var audio in list)
        {
            if (ct.IsCancellationRequested) break;
            results.Add(await GenerateForAudioAsync(audio, ct));
        }
        return results;
    }

    public async Task<List<TtsGenerateResult>> GenerateAllMissingAsync(CancellationToken ct = default)
    {
        var list = await _audioRepository.Query()
            .Where(a => !a.IsDeleted && !a.IsGenerated
                        && EdgeVoices.Map.Keys.Contains(a.LanguageCode))
            .OrderBy(a => a.PointOfInterestId)
            .ToListAsync(ct);

        _logger.LogInformation("Edge TTS: sẽ generate {Count} file audio", list.Count);

        var results = new List<TtsGenerateResult>();
        foreach (var audio in list)
        {
            if (ct.IsCancellationRequested) break;
            results.Add(await GenerateForAudioAsync(audio, ct));
        }
        return results;
    }

    // ─── Core ─────────────────────────────────────────────────────────────────

    private async Task<TtsGenerateResult> GenerateForAudioAsync(
        VK.Core.Entities.AudioContent audio, CancellationToken ct)
    {
        try
        {
            if (!EdgeVoices.Map.TryGetValue(audio.LanguageCode, out var voice))
                return new TtsGenerateResult(audio.Id, audio.PointOfInterestId, audio.LanguageCode,
                    false, null, $"Ngôn ngữ '{audio.LanguageCode}' không hỗ trợ (chỉ vi/en/ko)");

            // Đường dẫn output
            var relativePath = $"/audio/{audio.LanguageCode}/poi_{audio.PointOfInterestId}.mp3";
            var fullPath = Path.Combine(
                _audioRootPath, audio.LanguageCode,
                $"poi_{audio.PointOfInterestId}.mp3");

            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);

            // Gọi edge-tts CLI qua subprocess
            var success = await RunEdgeTtsAsync(audio.TextContent, voice, fullPath, ct);
            if (!success)
                return new TtsGenerateResult(audio.Id, audio.PointOfInterestId, audio.LanguageCode,
                    false, null, "edge-tts subprocess thất bại");

            // Tính duration từ file size (24kHz 48kbps ≈ 6000 bytes/giây)
            var fileInfo = new FileInfo(fullPath);
            var durationSec = (int)Math.Ceiling(fileInfo.Length / 6000.0);

            // Cập nhật DB
            audio.AudioFileUrl = relativePath;
            audio.IsGenerated = true;
            audio.DurationSeconds = durationSec;
            await _unitOfWork.SaveChangesAsync(ct);

            _logger.LogInformation(
                "TTS OK: POI {Poi} [{Lang}] → {Path} ({Kb} KB ~{Sec}s)",
                audio.PointOfInterestId, audio.LanguageCode,
                relativePath, fileInfo.Length / 1024, durationSec);

            return new TtsGenerateResult(audio.Id, audio.PointOfInterestId, audio.LanguageCode,
                true, relativePath, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "TTS generation failed: AudioContent {Id}", audio.Id);
            return new TtsGenerateResult(audio.Id, audio.PointOfInterestId, audio.LanguageCode,
                false, null, ex.Message);
        }
    }

    // ─── edge-tts subprocess ──────────────────────────────────────────────────
    // Dùng: python -m edge_tts --voice <voice> --text "<text>" --write-media <path>

    private async Task<bool> RunEdgeTtsAsync(
        string text, string voice, string outputPath, CancellationToken ct)
    {
        // Escape text: tránh lỗi với dấu ngoặc kép và ký tự đặc biệt trong shell
        var safeText = text
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"")
            .Replace("\n", " ")
            .Replace("\r", "");

        var pythonCandidates = new[]
        {
            Environment.GetEnvironmentVariable("TTS_PYTHON_EXECUTABLE"),
            "python3",
            "python"
        }
        .Where(x => !string.IsNullOrWhiteSpace(x))
        .Select(x => x!.Trim())
        .Distinct(StringComparer.OrdinalIgnoreCase)
        .ToList();

        string? lastError = null;

        foreach (var pythonExecutable in pythonCandidates)
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExecutable,
                Arguments = $"-m edge_tts --voice \"{voice}\" --text \"{safeText}\" --write-media \"{outputPath}\"",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = new Process { StartInfo = psi };
            var stderr = new System.Text.StringBuilder();

            process.ErrorDataReceived += (_, e) =>
            {
                if (e.Data != null) stderr.AppendLine(e.Data);
            };

            try
            {
                process.Start();
            }
            catch (Exception ex)
            {
                lastError = ex.Message;
                _logger.LogWarning("Could not start python executable '{PythonExe}' for edge-tts", pythonExecutable);
                continue;
            }

            process.BeginErrorReadLine();

            // Timeout 60s mỗi file
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(60));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                try { process.Kill(); } catch { /* best effort */ }
                _logger.LogWarning("edge-tts timeout: {Voice} → {Path}", voice, outputPath);
                return false;
            }

            if (process.ExitCode == 0)
            {
                return File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            }

            lastError = stderr.ToString();
            _logger.LogError(
                "edge-tts failed with executable '{PythonExe}', ExitCode={Code}: {Err}",
                pythonExecutable,
                process.ExitCode,
                lastError);
        }

        _logger.LogError(
            "edge-tts failed: no working python executable found. Last error: {LastError}",
            lastError ?? "unknown");
        return false;
    }
}
