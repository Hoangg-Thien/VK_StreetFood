using VK.Core.Entities;

namespace VK.Core.Interfaces;

public interface ITtsService
{
    /// <summary>
    /// Generate audio file from text using Google Cloud TTS
    /// </summary>
    /// <param name="text">Text content to convert to speech</param>
    /// <param name="languageCode">Language code (vi-VN, en-US, ko-KR)</param>
    /// <param name="voiceName">Voice name (vi-VN-Wavenet-A, en-US-Wavenet-C, ko-KR-Wavenet-A)</param>
    /// <returns>Audio file path (local or cloud URL)</returns>
    Task<string> GenerateAudioAsync(string text, string languageCode, string voiceName);

    /// <summary>
    /// Get available voices for a language
    /// </summary>
    /// <param name="languageCode">Language code (vi, en, ko)</param>
    /// <returns>List of available voice names</returns>
    Task<List<string>> GetAvailableVoicesAsync(string languageCode);

    /// <summary>
    /// Estimate audio duration from text length
    /// </summary>
    /// <param name="text">Text content</param>
    /// <returns>Estimated duration in seconds</returns>
    int EstimateDuration(string text);
}
