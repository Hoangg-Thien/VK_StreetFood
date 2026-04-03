using Microsoft.Maui.Devices;

namespace VK.Mobile.Models;

public class AppSettings
{
    private const int DevServerPort = 5089;

    // Android emulator uses 10.0.2.2, physical device can use adb reverse + 127.0.0.1.
    public static string ApiBaseUrl => $"http://{GetDevServerHost()}:{DevServerPort}/api/";
    public static string AudioBaseUrl => $"http://{GetDevServerHost()}:{DevServerPort}/";
    public const string OsrmBaseUrl = "https://router.project-osrm.org";
    public const string OfflineRoutePackageRelativeUrl = "offline/route-package";
    public const string OfflineRoutePackageFileName = "vkstreetfood.routes.json";

    // Geofencing
    public const double GeofenceRadiusMeters = 50.0;
    public const int LocationUpdateIntervalSeconds = 5;

    // Debounce & Cooldown chống spam geofence
    /// <summary>Sau khi khởi động app, bỏ qua trigger trong X ms đầu tiên (debounce).</summary>
    public const int GeofenceDebounceMs = 3_000;
    /// <summary>Mỗi POI chỉ trigger lại sau X phút (cooldown).</summary>
    public const int GeofenceCooldownMinutes = 5;

    // Map defaults – Phố Ẩm thực Vĩnh Khánh, Quận 4, TP.HCM
    public const double DefaultLatitude = 10.7619;
    public const double DefaultLongitude = 106.7022;
    public const int DefaultZoomLevel = 17;

    private static string GetDevServerHost()
    {
#if ANDROID
        return DeviceInfo.Current.DeviceType == DeviceType.Virtual ? "10.0.2.2" : "127.0.0.1";
#else
        return "localhost";
#endif
    }

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
