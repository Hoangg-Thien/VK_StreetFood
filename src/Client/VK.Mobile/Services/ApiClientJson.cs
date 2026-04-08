using System.Text.Json;

namespace VK.Mobile.Services;

internal static class ApiClientJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public static void EnsureBaseAddress(HttpClient httpClient)
    {
        if (httpClient.BaseAddress == null)
            httpClient.BaseAddress = new Uri(AppSettings.ApiBaseUrl);
    }
}
