namespace VK.Shared.Constants;

public static class LanguageConstants
{
    public const string Vietnamese = "vi";
    public const string English = "en";
    public const string Korean = "ko";

    public static readonly Dictionary<string, string> LanguageNames = new()
    {
        { Vietnamese, "Tiếng Việt" },
        { English, "English" },
        { Korean, "한국어" }
    };

    public static readonly string[] SupportedLanguages = { Vietnamese, English, Korean };
}

public static class ApiConstants
{
    public const string DefaultLanguage = LanguageConstants.Vietnamese;
    public const int QRCodeLength = 8;
    public const double DefaultLatitude = 10.0367; // Vĩnh Khánh location
    public const double DefaultLongitude = 105.7820;
    public const int MaxAudioDuration = 300; // 5 minutes
    public const int MaxDescriptionLength = 2000;
}

public static class FileConstants
{
    public const string AudioFileExtension = ".mp3";
    public const string ImageFileExtension = ".jpg";
    public const long MaxAudioFileSize = 10 * 1024 * 1024; // 10MB
    public const long MaxImageFileSize = 5 * 1024 * 1024;  // 5MB

    public static readonly string[] AllowedAudioExtensions = { ".mp3", ".wav", ".m4a" };
    public static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
}