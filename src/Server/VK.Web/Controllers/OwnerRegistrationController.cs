using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;

namespace VK.Web.Controllers;

public class OwnerRegistrationController : AdminBaseController
{
    private readonly IRepository<PoiOwnerRegistration> _ownerRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<OwnerRegistrationController> _logger;

    public OwnerRegistrationController(
        IRepository<PoiOwnerRegistration> ownerRegistrationRepository,
        IUnitOfWork unitOfWork,
        ILogger<OwnerRegistrationController> logger)
    {
        _ownerRegistrationRepository = ownerRegistrationRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string status = "pending")
    {
        var normalizedStatus = string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();

        var registrations = await _ownerRegistrationRepository.Query()
            .Include(r => r.User)
            .Include(r => r.PointOfInterest)
            .Include(r => r.Vendor)
            .Where(r => r.Status == normalizedStatus)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        ViewBag.Status = normalizedStatus;
        ViewBag.Registrations = registrations;
        return View("OwnerRegistrationPage");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? reviewNote)
    {
        try
        {
            var registration = await _ownerRegistrationRepository.Query()
                .Include(r => r.User)
                .Include(r => r.Vendor)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                TempData["Error"] = "Không tìm thấy đăng ký.";
                return RedirectToAction(nameof(Index));
            }

            registration.Status = "approved";
            registration.ReviewedAt = DateTime.UtcNow;
            registration.ReviewNote = reviewNote;

            registration.User.IsVerified = true;
            registration.User.Role = "poi_owner";

            if (registration.Vendor != null)
            {
                registration.Vendor.IsActive = true;
            }

            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = "Duyệt chủ quán thành công.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Approve owner registration failed");
            TempData["Error"] = "Không thể duyệt đăng ký.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string? reviewNote)
    {
        try
        {
            var registration = await _ownerRegistrationRepository.Query()
                .Include(r => r.User)
                .FirstOrDefaultAsync(r => r.Id == id);

            if (registration == null)
            {
                TempData["Error"] = "Không tìm thấy đăng ký.";
                return RedirectToAction(nameof(Index));
            }

            registration.Status = "rejected";
            registration.ReviewedAt = DateTime.UtcNow;
            registration.ReviewNote = reviewNote;
            registration.User.IsVerified = false;

            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = "Đã từ chối đăng ký.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Reject owner registration failed");
            TempData["Error"] = "Không thể từ chối đăng ký.";
        }

        return RedirectToAction(nameof(Index));
    }
}
