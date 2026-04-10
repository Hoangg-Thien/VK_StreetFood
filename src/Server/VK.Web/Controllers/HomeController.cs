using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using VK.Core.Interfaces;
using VK.Web.Models;
using VK.Web.Services;
using VK.Core.Entities;

namespace VK.Web.Controllers;

public class HomeController : Controller
{
    private readonly IRepository<User> _userRepository;
    private readonly IRepository<PointOfInterest> _poiRepository;
    private readonly IRepository<Vendor> _vendorRepository;
    private readonly IRepository<PoiOwnerRegistration> _ownerRegistrationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _config;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IRepository<User> userRepository,
        IRepository<PointOfInterest> poiRepository,
        IRepository<Vendor> vendorRepository,
        IRepository<PoiOwnerRegistration> ownerRegistrationRepository,
        IUnitOfWork unitOfWork,
        IConfiguration config,
        ILogger<HomeController> logger)
    {
        _userRepository = userRepository;
        _poiRepository = poiRepository;
        _vendorRepository = vendorRepository;
        _ownerRegistrationRepository = ownerRegistrationRepository;
        _unitOfWork = unitOfWork;
        _config = config;
        _logger = logger;
    }

    // GET / → Login page (no-cache so back button doesn't show it after logout)
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Index()
    {
        if (HttpContext.Session.GetString("UserLoggedIn") == "true")
        {
            var role = HttpContext.Session.GetString("UserRole");
            if (string.Equals(role, "poi_owner", StringComparison.OrdinalIgnoreCase))
                return RedirectToAction("Index", "Owner");

            return RedirectToAction("Index", "Dashboard");
        }

        return View();
    }

    // POST /Home/Login → check against Supabase Users table
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            // Admin credentials from appsettings.json
            var adminEmail = _config["AdminAuth:Email"] ?? "admin@vkstreetfood.vn";
            var adminPassword = _config["AdminAuth:Password"] ?? "Admin@2026";

            if (email == adminEmail && password == adminPassword)
            {
                // Look up the user record in the DB (for display info)
                var user = await _userRepository.Query()
                    .FirstOrDefaultAsync(u => u.Email == adminEmail && u.Role == "Admin");

                HttpContext.Session.SetString("UserLoggedIn", "true");
                HttpContext.Session.SetString("UserRole", "admin");
                HttpContext.Session.SetString("AdminLoggedIn", "true");
                HttpContext.Session.SetString("AdminUsername", user?.FullName ?? adminEmail.Split('@')[0]);
                HttpContext.Session.SetString("AdminEmail", adminEmail);

                TempData["InitAdminTab"] = "1";
                return RedirectToAction("Index", "Dashboard");
            }

            var owner = await _userRepository.Query()
                .Include(u => u.Vendor)
                .FirstOrDefaultAsync(u =>
                    !u.IsDeleted &&
                    u.Email == email &&
                    u.Role == "poi_owner");

            if (owner != null && PasswordHasher.Verify(password, owner.PasswordHash))
            {
                if (!owner.IsVerified)
                {
                    ViewBag.Error = "Tài khoản chủ quán đang chờ duyệt. Vui lòng đợi admin xác nhận.";
                    return View("Index");
                }

                owner.LastLoginAt = DateTime.UtcNow;
                await _unitOfWork.SaveChangesAsync();

                HttpContext.Session.SetString("UserLoggedIn", "true");
                HttpContext.Session.SetString("UserRole", "poi_owner");
                HttpContext.Session.SetString("UserEmail", owner.Email);
                HttpContext.Session.SetString("UserDisplayName", owner.FullName ?? owner.Email.Split('@')[0]);

                if (owner.VendorId.HasValue)
                    HttpContext.Session.SetInt32("VendorId", owner.VendorId.Value);

                HttpContext.Session.Remove("AdminLoggedIn");
                HttpContext.Session.Remove("AdminUsername");
                HttpContext.Session.Remove("AdminEmail");

                TempData["InitAdminTab"] = "1";
                return RedirectToAction("Index", "Owner");
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reach DB during login – falling back to config credentials.");

            // Fallback: allow login with pure config credentials even when DB is down
            var adminEmail = _config["AdminAuth:Email"] ?? "admin@vkstreetfood.vn";
            var adminPassword = _config["AdminAuth:Password"] ?? "Admin@2026";

            if (email == adminEmail && password == adminPassword)
            {
                HttpContext.Session.SetString("UserLoggedIn", "true");
                HttpContext.Session.SetString("UserRole", "admin");
                HttpContext.Session.SetString("AdminLoggedIn", "true");
                HttpContext.Session.SetString("AdminUsername", "Admin");
                HttpContext.Session.SetString("AdminEmail", adminEmail);
                TempData["InitAdminTab"] = "1";
                return RedirectToAction("Index", "Dashboard");
            }
        }

        ViewBag.Error = "Email hoặc mật khẩu không đúng.";
        return View("Index");
    }

    // GET /Home/Logout
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();

        // No-cache on the response so back button can't go back to admin
        Response.Headers["Cache-Control"] = "no-store, no-cache, must-revalidate, private, max-age=0";
        Response.Headers["Pragma"] = "no-cache";
        Response.Headers["Expires"] = "0";

        return RedirectToAction("Index", "Home");
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    [HttpGet("/open-app")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult OpenApp(string? target)
    {
        var deepLinkHost = NormalizeDeepLinkHost(target);
        if (string.IsNullOrWhiteSpace(deepLinkHost))
        {
            deepLinkHost = "pay";
        }

        var appDeepLink = $"vkstreetfood://{deepLinkHost}";
        var androidStoreUrl = _config["MobileAppLinks:AndroidDownloadUrl"]
            ?? _config["MobileAppLinks:AndroidStoreUrl"]
            ?? "https://vkstreetfood.vn/downloads/vkstreetfood.apk";
        var iosStoreUrl = _config["MobileAppLinks:IosDownloadUrl"]
            ?? _config["MobileAppLinks:IosStoreUrl"]
            ?? "https://testflight.apple.com/join/vkstreetfood";
        var webFallbackUrl = _config["MobileAppLinks:FallbackUrl"]
            ?? Url.Action("Index", "Home", null, Request.Scheme)
            ?? "/";

        ViewBag.AppDeepLink = appDeepLink;
        ViewBag.AndroidStoreUrl = androidStoreUrl;
        ViewBag.IosStoreUrl = iosStoreUrl;
        ViewBag.WebFallbackUrl = webFallbackUrl;

        return View("OpenApp");
    }

    [HttpGet]
    public async Task<IActionResult> OwnerRegister()
    {
        ViewBag.Pois = await _poiRepository.Query()
            .Where(p => p.IsActive && !p.IsDeleted && p.Id != 1)
            .OrderBy(p => p.Name)
            .ToListAsync();

        return View();
    }

    private static string NormalizeDeepLinkHost(string? host)
    {
        if (string.IsNullOrWhiteSpace(host))
        {
            return string.Empty;
        }

        var value = host.Trim().ToLowerInvariant();
        if (value.StartsWith("vkstreetfood://", StringComparison.OrdinalIgnoreCase))
        {
            value = value["vkstreetfood://".Length..].Trim('/');
        }

        if (value.Length is < 1 or > 50)
        {
            return string.Empty;
        }

        return Regex.IsMatch(value, "^[a-z0-9-]+$") ? value : string.Empty;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> OwnerRegister(
        string fullName,
        string email,
        string phoneNumber,
        string password,
        int pointOfInterestId,
        string? notes)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(fullName) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(phoneNumber) ||
                string.IsNullOrWhiteSpace(password))
            {
                TempData["OwnerRegisterError"] = "Vui lòng nhập đầy đủ thông tin.";
                return RedirectToAction(nameof(OwnerRegister));
            }

            var existed = await _userRepository.Query()
                .AnyAsync(u => !u.IsDeleted && u.Email == email);

            if (existed)
            {
                TempData["OwnerRegisterError"] = "Email đã được sử dụng.";
                return RedirectToAction(nameof(OwnerRegister));
            }

            var poi = await _poiRepository.Query()
                .FirstOrDefaultAsync(p => p.Id == pointOfInterestId && p.IsActive && !p.IsDeleted);

            if (poi == null)
            {
                TempData["OwnerRegisterError"] = "Không tìm thấy quán đăng ký.";
                return RedirectToAction(nameof(OwnerRegister));
            }

            var vendor = await _vendorRepository.Query()
                .FirstOrDefaultAsync(v => v.PointOfInterestId == poi.Id && !v.IsDeleted);

            if (vendor == null)
            {
                vendor = new Vendor
                {
                    Name = poi.Name,
                    Description = poi.Description,
                    ContactPerson = fullName,
                    PhoneNumber = phoneNumber,
                    Email = email,
                    PointOfInterestId = poi.Id,
                    ImageUrl = poi.ImageUrl,
                    IsActive = false
                };
                await _vendorRepository.AddAsync(vendor);
                await _unitOfWork.SaveChangesAsync();
            }

            var user = new User
            {
                Email = email,
                FullName = fullName,
                Role = "poi_owner",
                PasswordHash = PasswordHasher.Hash(password),
                IsVerified = false,
                VendorId = vendor.Id
            };
            await _userRepository.AddAsync(user);
            await _unitOfWork.SaveChangesAsync();

            await _ownerRegistrationRepository.AddAsync(new PoiOwnerRegistration
            {
                UserId = user.Id,
                PointOfInterestId = poi.Id,
                VendorId = vendor.Id,
                ShopName = poi.Name,
                ShopAddress = poi.Address,
                ContactPhone = phoneNumber,
                Notes = notes,
                Status = "pending"
            });

            await _unitOfWork.SaveChangesAsync();

            TempData["OwnerRegisterSuccess"] = "Đăng ký thành công. Vui lòng chờ admin duyệt tài khoản chủ quán.";
            return RedirectToAction(nameof(OwnerRegister));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Owner registration failed");
            TempData["OwnerRegisterError"] = "Đăng ký thất bại. Vui lòng thử lại.";
            return RedirectToAction(nameof(OwnerRegister));
        }
    }
}
