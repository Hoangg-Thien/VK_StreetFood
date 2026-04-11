using System.Net.Http.Headers;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace VK.Web.Services;

public sealed class SupabasePoiImageStorageService : IPoiImageStorageService
{
    private readonly HttpClient _httpClient;
    private readonly SupabaseStorageOptions _options;
    private readonly ILogger<SupabasePoiImageStorageService> _logger;

    public SupabasePoiImageStorageService(
        HttpClient httpClient,
        IOptions<SupabaseStorageOptions> options,
        ILogger<SupabasePoiImageStorageService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _options.IsConfigured;

    public async Task<string?> UploadPoiImageAsync(IFormFile file, string objectName, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return null;
        }

        var baseUrl = _options.Url.TrimEnd('/');
        var bucket = _options.Bucket.Trim();
        var uploadUrl = $"{baseUrl}/storage/v1/object/{Uri.EscapeDataString(bucket)}/{Uri.EscapeDataString(objectName)}";

        await using var fileStream = file.OpenReadStream();
        using var content = new StreamContent(fileStream);
        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? "application/octet-stream"
            : file.ContentType;
        content.Headers.ContentType = new MediaTypeHeaderValue(contentType);

        using var request = new HttpRequestMessage(HttpMethod.Post, uploadUrl)
        {
            Content = content
        };

        request.Headers.TryAddWithoutValidation("apikey", _options.ServiceRoleKey);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ServiceRoleKey);
        request.Headers.TryAddWithoutValidation("x-upsert", "true");

        try
        {
            using var response = await _httpClient.SendAsync(request, ct);
            if (!response.IsSuccessStatusCode)
            {
                var details = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning(
                    "Supabase upload failed. Status={StatusCode}, Bucket={Bucket}, Object={ObjectName}, Body={Body}",
                    response.StatusCode,
                    bucket,
                    objectName,
                    details);
                return null;
            }

            var publicBase = string.IsNullOrWhiteSpace(_options.PublicBaseUrl)
                ? baseUrl
                : _options.PublicBaseUrl!.TrimEnd('/');

            return $"{publicBase}/storage/v1/object/public/{Uri.EscapeDataString(bucket)}/{Uri.EscapeDataString(objectName)}";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Supabase upload threw exception for {ObjectName}", objectName);
            return null;
        }
    }
}
