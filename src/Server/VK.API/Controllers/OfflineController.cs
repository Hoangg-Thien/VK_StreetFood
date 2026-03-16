using Microsoft.AspNetCore.Mvc;

namespace VK.API.Controllers;

[ApiController]
[Route("api/offline")]
public class OfflineController : ControllerBase
{
    private const string MapFileName = "vkstreetfood.mbtiles";
    private readonly IWebHostEnvironment _environment;

    public OfflineController(IWebHostEnvironment environment)
    {
        _environment = environment;
    }

    private string OfflineFolderPath
        => Path.Combine(_environment.WebRootPath ?? Path.Combine(_environment.ContentRootPath, "wwwroot"), "offline");

    private string MapFilePath
        => Path.Combine(OfflineFolderPath, MapFileName);

    [HttpGet("map-package")]
    public IActionResult DownloadMapPackage()
    {
        if (!System.IO.File.Exists(MapFilePath))
        {
            return NotFound(new
            {
                message = "Offline map package not found. Upload vkstreetfood.mbtiles first.",
                uploadEndpoint = "/api/offline/map-package"
            });
        }

        return PhysicalFile(
            MapFilePath,
            "application/octet-stream",
            MapFileName,
            enableRangeProcessing: true);
    }

    [HttpGet("map-status")]
    public IActionResult GetMapStatus()
    {
        var hasMapPackage = System.IO.File.Exists(MapFilePath);
        var fileSizeBytes = hasMapPackage ? new FileInfo(MapFilePath).Length : 0;

        return Ok(new
        {
            hasMapPackage,
            fileName = MapFileName,
            fileSizeBytes,
            absolutePath = MapFilePath
        });
    }

    [HttpPost("map-package")]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadMapPackage(IFormFile file, CancellationToken ct)
    {
        if (file == null || file.Length <= 0)
        {
            return BadRequest(new { message = "File is required." });
        }

        var extension = Path.GetExtension(file.FileName);
        if (!string.Equals(extension, ".mbtiles", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = "Only .mbtiles files are supported." });
        }

        Directory.CreateDirectory(OfflineFolderPath);

        await using var target = System.IO.File.Create(MapFilePath);
        await file.CopyToAsync(target, ct);

        var fileSizeBytes = new FileInfo(MapFilePath).Length;
        return Ok(new
        {
            message = "Offline map package uploaded.",
            fileName = MapFileName,
            fileSizeBytes
        });
    }
}
