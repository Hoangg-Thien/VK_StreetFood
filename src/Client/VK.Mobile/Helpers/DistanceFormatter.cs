using System.Globalization;
using VK.Mobile.Services;

namespace VK.Mobile.Helpers;

/// <summary>
/// Formats a distance-in-km value into a localized, human-readable string.
/// Uses the same <c>NowPlayingDistanceMetersAwayFormat</c> / <c>NowPlayingDistanceKmAwayFormat</c>
/// resource keys that the NowPlayingViewModel and MainMapPage previously duplicated inline.
/// </summary>
internal static class DistanceFormatter
{
    /// <summary>
    /// Returns a localized, emoji-prefixed distance string, or <see cref="string.Empty"/>
    /// when <paramref name="distKm"/> is null or zero.
    /// </summary>
    public static string Format(double? distKm)
    {
        if (distKm is null or 0)
            return string.Empty;

        var L = LocalizationResourceManager.Instance;

        if (distKm < 0.1)
        {
            var text = string.Format(
                CultureInfo.CurrentCulture,
                L["NowPlayingDistanceMetersAwayFormat"],
                distKm.Value * 1000);
            return $"📍 {text}";
        }

        var kmText = string.Format(
            CultureInfo.CurrentCulture,
            L["NowPlayingDistanceKmAwayFormat"],
            distKm.Value);
        return $"📍 {kmText}";
    }
}
