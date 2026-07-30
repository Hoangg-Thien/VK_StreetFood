using VK.Core.Entities;
using VK.Shared.Constants;
using VK.Shared.DTOs;

namespace VK.API.Extensions;

/// <summary>
/// Shared localization utilities used across the API services.
/// </summary>
internal static class LocalizationHelper
{
    /// <summary>
    /// Strips the region sub-tag (e.g. "en-US" → "en") and lower-cases the result.
    /// Returns <see cref="LanguageConstants.Vietnamese"/> when the code is blank.
    /// </summary>
    public static string NormalizeLanguageCode(string? languageCode)
    {
        if (string.IsNullOrWhiteSpace(languageCode))
            return LanguageConstants.Vietnamese;

        var code = languageCode.Trim().ToLowerInvariant();
        var separatorIndex = code.IndexOfAny(['-', '_']);
        return separatorIndex > 0 ? code[..separatorIndex] : code;
    }

    /// <summary>
    /// Returns the best-matching <see cref="PointOfInterestTranslation"/> for the given
    /// language code, falling back to Vietnamese when no exact match exists.
    /// </summary>
    public static PointOfInterestTranslation? ResolvePoiTranslation(PointOfInterest poi, string languageCode)
    {
        var normalized = NormalizeLanguageCode(languageCode);
        return poi.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == normalized)
            ?? poi.Translations.FirstOrDefault(t => NormalizeLanguageCode(t.LanguageCode) == LanguageConstants.Vietnamese);
    }

    /// <summary>
    /// Overwrites the Name, Description and Address on <paramref name="dto"/> with
    /// the localized values from <paramref name="poi"/> when they are non-empty.
    /// </summary>
    public static void ApplyLocalizedPoiFields(POIListItemDto dto, PointOfInterest poi, string languageCode)
    {
        var translation = ResolvePoiTranslation(poi, languageCode);
        if (translation == null)
            return;

        if (!string.IsNullOrWhiteSpace(translation.Name))
            dto.Name = translation.Name;

        if (!string.IsNullOrWhiteSpace(translation.Description))
            dto.Description = translation.Description;

        if (!string.IsNullOrWhiteSpace(translation.Address))
            dto.Address = translation.Address;
    }
}
