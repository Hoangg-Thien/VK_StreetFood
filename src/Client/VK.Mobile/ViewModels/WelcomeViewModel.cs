using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using VK.Mobile.Services;

namespace VK.Mobile.ViewModels;

public partial class WelcomeViewModel : ObservableObject
{
    private readonly StorageService _storageService;

    [ObservableProperty] private string _selectedLanguage = "vi";

    // Màu nền button: trắng nếu được chọn, trong suốt nếu không
    public Color ViBackground => _selectedLanguage == "vi" ? Colors.White : Color.FromArgb("#00000000");
    public Color EnBackground => _selectedLanguage == "en" ? Colors.White : Color.FromArgb("#00000000");
    public Color KoBackground => _selectedLanguage == "ko" ? Colors.White : Color.FromArgb("#00000000");

    // Màu chữ button: cam (Primary) nếu được chọn, trắng nếu không
    public Color ViTextColor => _selectedLanguage == "vi" ? Color.FromArgb("#FF6B35") : Colors.White;
    public Color EnTextColor => _selectedLanguage == "en" ? Color.FromArgb("#FF6B35") : Colors.White;
    public Color KoTextColor => _selectedLanguage == "ko" ? Color.FromArgb("#FF6B35") : Colors.White;

    // Text trên màn hình tự đổi theo ngôn ngữ chọn
    public string Tagline => _selectedLanguage switch
    {
        "en" => "Discover Saigon's street food",
        "ko" => "사이공 길거리 음식 탐험",
        _ => "Khám phá ẩm thực đường phố Sài Gòn"
    };

    public string FeatureLocation => _selectedLanguage switch
    {
        "en" => "Find places near you",
        "ko" => "근처 장소 찾기",
        _ => "Tìm kiếm địa điểm gần bạn"
    };

    public string FeatureAudio => _selectedLanguage switch
    {
        "en" => "Multi-language audio guides",
        "ko" => "다국어 오디오 가이드 청취",
        _ => "Nghe hướng dẫn đa ngôn ngữ"
    };

    public string FeatureQR => _selectedLanguage switch
    {
        "en" => "Scan the QR code to listen to the narration.",
        "ko" => "QR 코드를 스캔하여 내레이션을 들어보세요.",
        _ => "Quét QR để nghe thuyết minh"
    };

    public string GetStartedText => _selectedLanguage switch
    {
        "en" => "START EXPLORING",
        "ko" => "탐험 시작하기",
        _ => "BẮT ĐẦU KHÁM PHÁ"
    };

    partial void OnSelectedLanguageChanged(string value)
    {
        OnPropertyChanged(nameof(ViBackground));
        OnPropertyChanged(nameof(EnBackground));
        OnPropertyChanged(nameof(KoBackground));
        OnPropertyChanged(nameof(ViTextColor));
        OnPropertyChanged(nameof(EnTextColor));
        OnPropertyChanged(nameof(KoTextColor));
        OnPropertyChanged(nameof(Tagline));
        OnPropertyChanged(nameof(FeatureLocation));
        OnPropertyChanged(nameof(FeatureAudio));
        OnPropertyChanged(nameof(FeatureQR));
        OnPropertyChanged(nameof(GetStartedText));
    }

    public WelcomeViewModel(StorageService storageService)
    {
        _storageService = storageService;
        // Khởi tạo từ ngôn ngữ đang được lưu (Preferences) thay vì luôn mặc định "vi"
        _selectedLanguage = LocalizationResourceManager.Instance.CurrentLanguage;
    }

    [RelayCommand]
    void SelectLanguage(string lang)
    {
        SelectedLanguage = lang;
        // Áp dụng ngôn ngữ ngay để LocalizationResourceManager cập nhật toàn app
        LocalizationResourceManager.Instance.SetLanguage(lang);
    }

    [RelayCommand]
    async Task GetStarted()
    {
        // Language đã được lưu bởi LocalizationResourceManager.SetLanguage (dùng Preferences)
        // ngay khi SelectLanguage được gọi → không cần lưu SecureStorage nữa
        await Shell.Current.GoToAsync("//MainMap");
    }
}
