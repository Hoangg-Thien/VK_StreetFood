using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Infrastructure.Data;
using VK.Shared.DTOs;
using VK.Core.Entities;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class QRCodeController : ControllerBase
{
    private readonly VKStreetFoodDbContext _context;
    private readonly ILogger<QRCodeController> _logger;

    public QRCodeController(VKStreetFoodDbContext context, ILogger<QRCodeController> logger)
    {
        _context = context;
        _logger = logger;
    }

    /// <summary>
    /// Scan QR code and get POI information with audio content
    /// </summary>
    /// <param name="qrCode">QR code string (e.g., VK-OC-OANH)</param>
    /// <param name="languageCode">Language preference (vi/en/ko)</param>
    /// <returns>POI details with audio content and vendor information</returns>
    [HttpGet("scan/{qrCode}")]
    public async Task<ActionResult<QRScanResponseDto>> ScanQRCode(
        string qrCode,
        [FromQuery] string languageCode = "vi")
    {
        try
        {
            // Find POI by QR code
            var poi = await _context.PointsOfInterest
                .Include(p => p.Category)
                .Include(p => p.AudioContents)
                .Include(p => p.Vendors)
                    .ThenInclude(v => v.Products)
                .Include(p => p.Tags)
                .FirstOrDefaultAsync(p => p.QRCode == qrCode && !p.IsDeleted);

            if (poi == null)
            {
                return NotFound(new { message = "QR code không hợp lệ hoặc không tồn tại" });
            }

            // Get audio content for requested language
            var audio = poi.AudioContents
                .FirstOrDefault(a => a.LanguageCode == languageCode);

            // Fallback to Vietnamese if requested language not available
            if (audio == null)
            {
                audio = poi.AudioContents.FirstOrDefault(a => a.LanguageCode == "vi");
            }

            await _context.SaveChangesAsync();

            var response = new QRScanResponseDto
            {
                PoiId = poi.Id,
                Name = poi.Name,
                Description = poi.Description,
                Latitude = poi.Latitude,
                Longitude = poi.Longitude,
                Address = poi.Address,
                ImageUrl = poi.ImageUrl,
                AverageRating = poi.AverageRating,
                TotalRatings = poi.TotalRatings,
                Category = poi.Category?.Name,
                Tags = poi.Tags.Select(t => t.Name).ToList(),
                Audio = audio != null ? new AudioContentDto
                {
                    AudioId = audio.Id,
                    LanguageCode = audio.LanguageCode,
                    AudioFileUrl = audio.AudioFileUrl,
                    TextContent = audio.TextContent,
                    DurationInSeconds = audio.DurationInSeconds
                } : null,
                Vendors = poi.Vendors.Select(v => new VendorDto
                {
                    VendorId = v.Id,
                    Name = v.Name,
                    Description = v.Description,
                    PhoneNumber = v.PhoneNumber,
                    AverageRating = v.AverageRating,
                    Products = v.Products.Where(p => p.IsAvailable).Select(p => new ProductDto
                    {
                        ProductId = p.Id,
                        Name = p.Name,
                        Description = p.Description,
                        Price = p.Price,
                        ImageUrl = p.ImageUrl
                    }).ToList()
                }).ToList()
            };

            _logger.LogInformation("QR Code {QRCode} scanned successfully for POI {PoiId}", qrCode, poi.Id);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error scanning QR code {QRCode}", qrCode);
            return StatusCode(500, new { message = "Lỗi khi quét QR code" });
        }
    }

    /// <summary>
    /// Validate QR code existence
    /// </summary>
    [HttpGet("validate/{qrCode}")]
    public async Task<ActionResult<bool>> ValidateQRCode(string qrCode)
    {
        var exists = await _context.PointsOfInterest
            .AnyAsync(p => p.QRCode == qrCode && !p.IsDeleted);

        return Ok(new { valid = exists });
    }
}
