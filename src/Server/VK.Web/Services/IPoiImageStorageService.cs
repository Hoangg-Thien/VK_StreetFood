using Microsoft.AspNetCore.Http;

namespace VK.Web.Services;

public interface IPoiImageStorageService
{
    bool IsConfigured { get; }

    Task<string?> UploadPoiImageAsync(IFormFile file, string objectName, CancellationToken ct = default);
}
