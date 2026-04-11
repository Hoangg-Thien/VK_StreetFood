namespace VK.Web.Services;

public sealed class SupabaseStorageOptions
{
    public string Url { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string Bucket { get; set; } = "poi-images";
    public string? PublicBaseUrl { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Url)
        && !string.IsNullOrWhiteSpace(ServiceRoleKey)
        && !string.IsNullOrWhiteSpace(Bucket);
}
