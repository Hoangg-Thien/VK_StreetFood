namespace VK.Web.Services;

public interface ITextTranslationService
{
    Task<string> TranslateAsync(string text, string sourceLanguage, string targetLanguage, CancellationToken ct = default);
}
