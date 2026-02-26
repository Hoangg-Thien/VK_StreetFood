namespace VK.Mobile.Models;

public class AppSettings
{
    public const string ApiBaseUrl = "http://10.0.2.2:5089/api/";
    public const string AudioBaseUrl = "http://10.0.2.2:5089/";

    // Geofencing
    public const double GeofenceRadiusMeters = 50.0;
    public const int LocationUpdateIntervalSeconds = 5;

    // Debounce & Cooldown chống spam geofence
    /// <summary>Sau khi khởi động app, bỏ qua trigger trong X ms đầu tiên (debounce).</summary>
    public const int GeofenceDebounceMs = 3_000;
    /// <summary>Mỗi POI chỉ trigger lại sau X phút (cooldown).</summary>
    public const int GeofenceCooldownMinutes = 10;

    // Map defaults
    public const double DefaultLatitude = 10.761;
    public const double DefaultLongitude = 106.703;
    public const int DefaultZoomLevel = 15;

    // Languages
    public static readonly string[] SupportedLanguages = { "vi", "en", "ko" };

    public static readonly Dictionary<string, string> LanguageNames = new()
    {
        { "vi", "Tiếng Việt" },
        { "en", "English" },
        { "ko", "한국어" }
    };
}
