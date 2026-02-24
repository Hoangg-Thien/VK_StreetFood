using Google.Cloud.TextToSpeech.V1;
using VK.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace VK.Infrastructure.ExternalServices;

public class GoogleCloudTtsService : ITtsService
{
    private readonly TextToSpeechClient _client;
    private readonly IConfiguration _configuration;
    private readonly ILogger<GoogleCloudTtsService> _logger;
    private readonly string _audioOutputPath;

    public GoogleCloudTtsService(
        IConfiguration configuration,
        ILogger<GoogleCloudTtsService> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Initialize Google Cloud TTS client
        try
        {
            _client = TextToSpeechClient.Create();
            _logger.LogInformation("Google Cloud TTS client initialized successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize Google Cloud TTS client");
            throw;
        }

        // Get audio output path from configuration
        _audioOutputPath = _configuration["TtsSettings:AudioOutputPath"] 
                           ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "audio");

        // Create directory if not exists
        if (!Directory.Exists(_audioOutputPath))
        {
            Directory.CreateDirectory(_audioOutputPath);
            _logger.LogInformation("Created audio output directory: {Path}", _audioOutputPath);
        }
    }

    public async Task<string> GenerateAudioAsync(string text, string languageCode, string voiceName)
    {
        try
        {
            _logger.LogInformation("Generating audio for language {Language}, voice {Voice}", languageCode, voiceName);

            // Create synthesis input
            var input = new SynthesisInput
            {
                Text = text
            };

            // Build voice parameters
            var voice = new VoiceSelectionParams
            {
                LanguageCode = languageCode,
                Name = voiceName,
                SsmlGender = SsmlVoiceGender.Female // Default to female voice
            };

            // Configure audio format
            var audioConfig = new AudioConfig
            {
                AudioEncoding = AudioEncoding.Mp3,
                SpeakingRate = 1.0, // Normal speed
                Pitch = 0.0, // Normal pitch
                VolumeGainDb = 0.0 // Normal volume
            };

            // Perform text-to-speech
            var response = await _client.SynthesizeSpeechAsync(input, voice, audioConfig);

            // Generate unique filename
            var fileName = $"{languageCode}_{Guid.NewGuid()}.mp3";
            var filePath = Path.Combine(_audioOutputPath, fileName);

            // Write audio content to file
            await File.WriteAllBytesAsync(filePath, response.AudioContent.ToByteArray());

            _logger.LogInformation("Audio file generated successfully: {FilePath}", filePath);

            // Return relative URL path for client consumption
            var relativeUrl = $"/audio/{fileName}";
            return relativeUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating audio for text: {Text}", text.Substring(0, Math.Min(50, text.Length)));
            throw;
        }
    }

    public async Task<List<string>> GetAvailableVoicesAsync(string languageCode)
    {
        try
        {
            // Map short codes to full language codes
            var fullLanguageCode = languageCode.ToLower() switch
            {
                "vi" => "vi-VN",
                "en" => "en-US",
                "ko" => "ko-KR",
                _ => languageCode
            };

            var request = new ListVoicesRequest
            {
                LanguageCode = fullLanguageCode
            };

            var response = await _client.ListVoicesAsync(request);

            // Filter Wavenet voices (highest quality)
            var wavenetVoices = response.Voices
                .Where(v => v.Name.Contains("Wavenet"))
                .Select(v => v.Name)
                .ToList();

            _logger.LogInformation("Found {Count} Wavenet voices for {Language}", wavenetVoices.Count, fullLanguageCode);

            return wavenetVoices;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error fetching available voices for {Language}", languageCode);
            throw;
        }
    }

    public int EstimateDuration(string text)
    {
        // Average speaking rate: 150 words per minute
        // Average word length: 5 characters
        const int avgWordsPerMinute = 150;
        const int avgCharsPerWord = 5;

        int estimatedWords = text.Length / avgCharsPerWord;
        double estimatedMinutes = (double)estimatedWords / avgWordsPerMinute;
        int estimatedSeconds = (int)Math.Ceiling(estimatedMinutes * 60);

        // Add 10% buffer for pauses and pronunciation
        return (int)(estimatedSeconds * 1.1);
    }
}
