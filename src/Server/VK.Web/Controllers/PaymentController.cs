using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Web.Models;

namespace VK.Web.Controllers;

public class PaymentController : AdminBaseController
{
    private readonly IRepository<QrPaymentConfig> _paymentConfigRepository;
    private readonly IRepository<Analytics> _analyticsRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IRepository<QrPaymentConfig> paymentConfigRepository,
        IRepository<Analytics> analyticsRepository,
        IUnitOfWork unitOfWork,
        ILogger<PaymentController> logger)
    {
        _paymentConfigRepository = paymentConfigRepository;
        _analyticsRepository = analyticsRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IActionResult> Index(string? status, DateTime? fromDate, DateTime? toDate)
    {
        var config = await EnsureConfigAsync();

        var normalizedStatus = NormalizeStatusFilter(status);
        var fromUtc = fromDate?.Date.ToUniversalTime();
        var toExclusiveUtc = toDate?.Date.AddDays(1).ToUniversalTime();

        var paymentEventsQuery = _analyticsRepository.Query()
            .AsNoTracking()
            .Include(a => a.Tourist)
            .Include(a => a.PointOfInterest)
            .Where(a => a.EventType == "qr_payment" || a.EventType == "qr_payment_success" || a.EventType == "qr_payment_failed");

        if (!string.IsNullOrWhiteSpace(normalizedStatus))
        {
            paymentEventsQuery = paymentEventsQuery.Where(a => a.EventType == normalizedStatus);
        }

        if (fromUtc.HasValue)
        {
            paymentEventsQuery = paymentEventsQuery.Where(a => a.EventTimestamp >= fromUtc.Value);
        }

        if (toExclusiveUtc.HasValue)
        {
            paymentEventsQuery = paymentEventsQuery.Where(a => a.EventTimestamp < toExclusiveUtc.Value);
        }

        var transactions = await paymentEventsQuery
            .OrderByDescending(a => a.EventTimestamp)
            .Take(150)
            .Select(a => new PaymentHistoryItemViewModel
            {
                OccurredAt = a.EventTimestamp,
                DeviceId = a.Tourist != null ? a.Tourist.DeviceId : string.Empty,
                Status = MapStatusLabel(a.EventType),
                StatusCode = a.EventType,
                PoiName = a.PointOfInterest != null ? a.PointOfInterest.Name : string.Empty
            })
            .ToListAsync();

        var model = new PaymentConfigEditViewModel
        {
            DefaultAmountVnd = config.DefaultAmountVnd,
            DeepLinkName = string.IsNullOrWhiteSpace(config.DeepLinkName) ? "pay" : config.DeepLinkName,
            QrTtlMinutes = config.QrTtlMinutes > 0 ? config.QrTtlMinutes : 15,
            SelectedStatus = normalizedStatus,
            FromDate = fromDate,
            ToDate = toDate,
            PaymentHistory = transactions
        };

        return View("PaymentPage", model);
    }

    [HttpPost]
    public async Task<IActionResult> Update(PaymentConfigEditViewModel model)
    {
        var normalizedHost = NormalizeDeepLinkHost(model.DeepLinkName);
        var ttl = model.QrTtlMinutes;
        var amount = model.DefaultAmountVnd;

        if (string.IsNullOrWhiteSpace(normalizedHost))
        {
            TempData["Error"] = "Deep link host không hợp lệ. Chỉ dùng chữ thường, số hoặc dấu gạch ngang.";
            return RedirectToAction(nameof(Index));
        }

        if (ttl is < 1 or > 1440)
        {
            TempData["Error"] = "Thời hạn QR phải trong khoảng 1-1440 phút.";
            return RedirectToAction(nameof(Index));
        }

        if (amount < 0)
        {
            TempData["Error"] = "Số tiền mặc định không được âm.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            var config = await EnsureConfigAsync();
            config.DeepLinkName = normalizedHost;
            config.QrTtlMinutes = ttl;
            config.DefaultAmountVnd = amount;

            await _unitOfWork.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật cấu hình thanh toán QR.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating payment config");
            TempData["Error"] = "Có lỗi khi lưu cấu hình thanh toán.";
        }

        return RedirectToAction(nameof(Index));
    }

    private async Task<QrPaymentConfig> EnsureConfigAsync()
    {
        var config = await _paymentConfigRepository.Query()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        if (config != null)
        {
            return config;
        }

        config = new QrPaymentConfig
        {
            DefaultAmountVnd = 0,
            DeepLinkName = "pay",
            QrTtlMinutes = 15
        };

        await _paymentConfigRepository.AddAsync(config);
        await _unitOfWork.SaveChangesAsync();
        return config;
    }

    private static string NormalizeDeepLinkHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var normalized = host.Trim().ToLowerInvariant();
        if (normalized.StartsWith("vkstreetfood://", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["vkstreetfood://".Length..].Trim('/');
        }

        if (normalized.Length is < 1 or > 50)
        {
            return string.Empty;
        }

        foreach (var c in normalized)
        {
            var isLetterOrDigit = char.IsLetterOrDigit(c);
            if (!isLetterOrDigit && c != '-')
            {
                return string.Empty;
            }
        }

        return normalized;
    }

    private static string NormalizeStatusFilter(string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return string.Empty;
        }

        return status.Trim().ToLowerInvariant() switch
        {
            "qr_payment" => "qr_payment",
            "qr_payment_success" => "qr_payment_success",
            "qr_payment_failed" => "qr_payment_failed",
            _ => string.Empty
        };
    }

    private static string MapStatusLabel(string eventType)
        => eventType switch
        {
            "qr_payment_success" => "Thành công",
            "qr_payment_failed" => "Thất bại",
            "qr_payment" => "Khởi tạo",
            _ => "Không xác định"
        };
}
