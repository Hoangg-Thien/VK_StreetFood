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
        // Đổi cả LocalizationResourceManager (XAML binding) lẫn CultureInfo
        LocalizationResourceManager.Instance.SetLanguage(languageCode);
        CurrentCulture = CultureInfo.CurrentCulture;
    }

    public string GetString(string key)
    {
        return AppResources.ResourceManager.GetString(key, CurrentCulture) ?? key;
    }
}
