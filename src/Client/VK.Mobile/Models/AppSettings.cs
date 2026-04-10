using Microsoft.Maui.Devices;

namespace VK.Mobile.Models;

public class AppSettings
{
    private const int DevServerPort = 5089;
    private const string ProductionApiBaseUrl = "https://vk-api-51j1.onrender.com/api/";
    private const string ProductionAudioBaseUrl = "https://vk-api-51j1.onrender.com/";

    // Android emulator uses 10.0.2.2, physical device can use adb reverse + 127.0.0.1.
    public static string ApiBaseUrl
    {
        get
        {
#if DEBUG
            return $"http://{GetDevServerHost()}:{DevServerPort}/api/";
#else
            return ProductionApiBaseUrl;
#endif
        }
    }

    public static string AudioBaseUrl
    {
        get
        {
#if DEBUG
            return $"http://{GetDevServerHost()}:{DevServerPort}/";
#else
            return ProductionAudioBaseUrl;
#endif
        }
    }
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
