using System.Globalization;
using VK.Mobile.Resources.Strings;

namespace VK.Mobile.Services;

public interface ILocalizationService
{
    void SetCulture(string languageCode);
    string GetString(string key);
    CultureInfo CurrentCulture { get; }
}

public class LocalizationService : ILocalizationService
{
    public CultureInfo CurrentCulture { get; private set; } = CultureInfo.CurrentCulture;

    public void SetCulture(string languageCode)
    {
        var culture = languageCode switch
        {
            "vi" => new CultureInfo("vi-VN"),
            "en" => new CultureInfo("en-US"),
            "ko" => new CultureInfo("ko-KR"),
            _ => new CultureInfo("vi-VN")
        };

        CurrentCulture = culture;
        CultureInfo.CurrentCulture = culture;
        CultureInfo.CurrentUICulture = culture;
        CultureInfo.DefaultThreadCurrentCulture = culture;
        CultureInfo.DefaultThreadCurrentUICulture = culture;

        AppResources.Culture = culture;
    }

    public string GetString(string key)
    {
        return AppResources.ResourceManager.GetString(key, CurrentCulture) ?? key;
    }
}
