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

    // Map defaults – Phố Ẩm thực Vĩnh Khánh, Quận 4, TP.HCM
    public const double DefaultLatitude = 10.7619;
    public const double DefaultLongitude = 106.7022;
    public const int DefaultZoomLevel = 17;

#if DEBUG
    // Tọa độ fake cho emulator (Phố Vĩnh Khánh Q4)
    public const bool UseMockLocation = true;
    public const double MockLatitude = 10.75931;
    public const double MockLongitude = 106.70701;
#else
    public const bool UseMockLocation = false;
    public const double MockLatitude = DefaultLatitude;
    public const double MockLongitude = DefaultLongitude;
#endif

    // Languages
    public static readonly string[] SupportedLanguages = { "vi", "en", "ko" };

    public static readonly Dictionary<string, string> LanguageNames = new()
    {
        { "vi", "Tiếng Việt" },
        { "en", "English" },
        { "ko", "한국어" }
    };
}
