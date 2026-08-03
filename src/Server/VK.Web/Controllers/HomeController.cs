using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.RegularExpressions;
using VK.Core.Interfaces;
using VK.Web.Models;
using VK.Shared.Security;
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

    // POST /Home/Login → check against AdminAuth config or Supabase Users table
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(string email, string password)
    {
        try
        {
            email = email?.Trim() ?? string.Empty;
            password = password ?? string.Empty;

            var configAdminEmail = (_config["AdminAuth:Email"] ?? "admin@vkstreetfood.vn").Trim();
            var configAdminPassword = _config["AdminAuth:Password"] ?? "Admin@2026";

            // 1. Direct AdminAuth match
            if (string.Equals(email, configAdminEmail, StringComparison.OrdinalIgnoreCase) &&
                (password == configAdminPassword || password == configAdminPassword.Trim()))
            {
                User? adminUser = null;
                try
                {
                    adminUser = await _userRepository.Query()
                        .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == configAdminEmail.ToLower());
                }
                catch
                {
                    // DB query may fail if DB is initializing
                }

                HttpContext.Session.SetString("UserLoggedIn", "true");
                HttpContext.Session.SetString("UserRole", "admin");
                HttpContext.Session.SetString("AdminLoggedIn", "true");
                HttpContext.Session.SetString("AdminUsername", adminUser?.FullName ?? configAdminEmail.Split('@')[0]);
                HttpContext.Session.SetString("AdminEmail", configAdminEmail);

                TempData["InitAdminTab"] = "1";

                if (adminUser != null)
                {
                    adminUser.LastLoginAt = DateTime.UtcNow;
                    try { await _unitOfWork.SaveChangesAsync(); } catch { }
                }

                return RedirectToAction("Index", "Dashboard");
            }

            // 2. Database user authentication
            var user = await _userRepository.Query()
                .Include(u => u.Vendor)
                .FirstOrDefaultAsync(u => !u.IsDeleted && u.Email.ToLower() == email.ToLower());

            if (user != null && !string.IsNullOrWhiteSpace(user.PasswordHash) && PasswordHasher.Verify(password, user.PasswordHash))
            {
                if (string.Equals(user.Role, "Admin", StringComparison.OrdinalIgnoreCase))
                {
                    HttpContext.Session.SetString("UserLoggedIn", "true");
                    HttpContext.Session.SetString("UserRole", "admin");
                    HttpContext.Session.SetString("AdminLoggedIn", "true");
                    HttpContext.Session.SetString("AdminUsername", user.FullName ?? email.Split('@')[0]);
                    HttpContext.Session.SetString("AdminEmail", user.Email);

                    TempData["InitAdminTab"] = "1";

                    user.LastLoginAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();

                    return RedirectToAction("Index", "Dashboard");
                }
                else if (string.Equals(user.Role, "poi_owner", StringComparison.OrdinalIgnoreCase))
                {
                    if (!user.IsVerified)
                    {
                        ViewBag.Error = "Tài khoản chủ quán đang chờ duyệt. Vui lòng đợi admin xác nhận.";
                        return View("Index");
                    }

                    user.LastLoginAt = DateTime.UtcNow;
                    await _unitOfWork.SaveChangesAsync();

                    HttpContext.Session.SetString("UserLoggedIn", "true");
                    HttpContext.Session.SetString("UserRole", "poi_owner");
                    HttpContext.Session.SetString("UserEmail", user.Email);
                    HttpContext.Session.SetString("UserDisplayName", user.FullName ?? user.Email.Split('@')[0]);

                    if (user.VendorId.HasValue)
                        HttpContext.Session.SetInt32("VendorId", user.VendorId.Value);

                    HttpContext.Session.Remove("AdminLoggedIn");
                    HttpContext.Session.Remove("AdminUsername");
                    HttpContext.Session.Remove("AdminEmail");

                    TempData["InitAdminTab"] = "1";
                    return RedirectToAction("Index", "Owner");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Exception during login attempt for {Email}", email);
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

            await _unitOfWork.ExecuteInTransactionAsync(async () =>
            {
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
                }

                var user = new User
                {
                    Email = email,
                    FullName = fullName,
                    Role = "poi_owner",
                    PasswordHash = PasswordHasher.Hash(password),
                    IsVerified = false,
                    Vendor = vendor
                };
                await _userRepository.AddAsync(user);

                await _ownerRegistrationRepository.AddAsync(new PoiOwnerRegistration
                {
                    User = user,
                    PointOfInterestId = poi.Id,
                    Vendor = vendor,
                    ShopName = poi.Name,
                    ShopAddress = poi.Address,
                    ContactPhone = phoneNumber,
                    Notes = notes,
                    Status = "pending"
                });

                await _unitOfWork.SaveChangesAsync();
            });

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
