using System.ComponentModel;
using System.Globalization;
using VK.Mobile.Resources.Strings;

namespace VK.Mobile.Services;

/// <summary>
/// Singleton dùng cho XAML binding đa ngôn ngữ realtime.
/// Khi đổi ngôn ngữ, tất cả {Binding Source={x:Static loc:L.Instance}, Path=[key]}
/// sẽ tự cập nhật nhờ INotifyPropertyChanged.
/// </summary>
public class LocalizationResourceManager : INotifyPropertyChanged
{
    public static readonly LocalizationResourceManager Instance = new();

    private LocalizationResourceManager()
    {
        // Khởi tạo ngôn ngữ từ Preferences
        CurrentLanguage = Preferences.Get("Language", "vi");
        ApplyCulture(CurrentLanguage);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Mã ngôn ngữ hiện tại: "vi", "en", "ko"</summary>
    public string CurrentLanguage { get; private set; } = "vi";

    public string this[string key]
    {
        get
        {
            var value = AppResources.ResourceManager.GetString(key, AppResources.Culture);
            return string.IsNullOrEmpty(value) ? key : value;
        }
    }

    /// <summary>Đổi ngôn ngữ và notify tất cả bindings.</summary>
    public void SetLanguage(string languageCode)
    {
        if (CurrentLanguage == languageCode) return;
        CurrentLanguage = languageCode;
        Preferences.Set("Language", languageCode);
        ApplyCulture(languageCode);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
    }

    private static void ApplyCulture(string languageCode)
    {
        var culture = languageCode switch
        {
            "en" => new CultureInfo("en-US"),
            "ko" => new CultureInfo("ko-KR"),
            _ => new CultureInfo("vi-VN")
        };
        AppResources.Culture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;
    }
}
