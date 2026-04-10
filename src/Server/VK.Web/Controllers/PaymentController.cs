using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Web.Models;

namespace VK.Web.Controllers;

public class PaymentController : AdminBaseController
{
    private readonly IRepository<QrPaymentConfig> _paymentConfigRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        IRepository<QrPaymentConfig> paymentConfigRepository,
        IUnitOfWork unitOfWork,
        ILogger<PaymentController> logger)
    {
        _paymentConfigRepository = paymentConfigRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var config = await EnsureConfigAsync();

        var model = new PaymentConfigEditViewModel
        {
            DefaultAmountVnd = config.DefaultAmountVnd,
            DeepLinkName = string.IsNullOrWhiteSpace(config.DeepLinkName) ? "pay" : config.DeepLinkName,
            QrTtlMinutes = config.QrTtlMinutes > 0 ? config.QrTtlMinutes : 15
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
}
