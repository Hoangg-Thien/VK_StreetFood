using System.Text.Json;

namespace VK.Web.Services;

public sealed class GoogleTextTranslationService : ITextTranslationService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<GoogleTextTranslationService> _logger;

    public GoogleTextTranslationService(HttpClient httpClient, ILogger<GoogleTextTranslationService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        if (string.Equals(sourceLanguage, targetLanguage, StringComparison.OrdinalIgnoreCase))
            return text;

        try
        {
            var encodedText = Uri.EscapeDataString(text);
            var url =
                $"https://translate.googleapis.com/translate_a/single?client=gtx&sl={sourceLanguage}&tl={targetLanguage}&dt=t&q={encodedText}";

            using var response = await _httpClient.GetAsync(url, ct);
            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Translate request failed with status {StatusCode} for {Source}->{Target}",
                    response.StatusCode,
                    sourceLanguage,
                    targetLanguage);
                return text;
            }

            var payload = await response.Content.ReadAsStringAsync(ct);
            var translated = ExtractTranslatedText(payload);
            return string.IsNullOrWhiteSpace(translated) ? text : translated;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Translate failed for {Source}->{Target}", sourceLanguage, targetLanguage);
            return text;
        }
    }

    private static string? ExtractTranslatedText(string json)
    {
        using var document = JsonDocument.Parse(json);

        if (document.RootElement.ValueKind != JsonValueKind.Array || document.RootElement.GetArrayLength() == 0)
            return null;

        var sentenceArray = document.RootElement[0];
        if (sentenceArray.ValueKind != JsonValueKind.Array)
            return null;

        var parts = new List<string>();
        foreach (var sentence in sentenceArray.EnumerateArray())
        {
            if (sentence.ValueKind != JsonValueKind.Array || sentence.GetArrayLength() == 0)
                continue;

            var piece = sentence[0].GetString();
            if (!string.IsNullOrEmpty(piece))
            {
                parts.Add(piece);
            }
        }

        return parts.Count == 0 ? null : string.Concat(parts);
    }
}
