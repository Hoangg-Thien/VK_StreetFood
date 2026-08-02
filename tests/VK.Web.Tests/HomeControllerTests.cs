using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Shared.Security;
using VK.Web.Controllers;

namespace VK.Web.Tests;

public class HomeControllerTests
{
    [Fact]
    public async Task Login_ValidAdmin_RedirectsToDashboard()
    {
        using var context = CreateContext();
        var controller = CreateController(context, out var sessionMock);

        var adminUser = new User
        {
            Email = "admin@test.com",
            PasswordHash = PasswordHasher.Hash("password123"),
            Role = "Admin",
            IsVerified = true
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        var result = await controller.Login("admin@test.com", "password123");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Dashboard", redirect.ControllerName);
        Assert.Equal("Index", redirect.ActionName);

        sessionMock.Verify(s => s.Set(It.Is<string>(k => k == "UserRole"), It.IsAny<byte[]>()), Times.Once);
        sessionMock.Verify(s => s.Set(It.Is<string>(k => k == "AdminLoggedIn"), It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task Login_ValidOwner_RedirectsToOwnerDashboard()
    {
        using var context = CreateContext();
        var controller = CreateController(context, out var sessionMock);

        var ownerUser = new User
        {
            Email = "owner@test.com",
            PasswordHash = PasswordHasher.Hash("password123"),
            Role = "poi_owner",
            IsVerified = true,
            VendorId = 1
        };
        context.Users.Add(ownerUser);
        await context.SaveChangesAsync();

        var result = await controller.Login("owner@test.com", "password123");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Owner", redirect.ControllerName);
        Assert.Equal("Index", redirect.ActionName);

        sessionMock.Verify(s => s.Set(It.Is<string>(k => k == "UserRole"), It.IsAny<byte[]>()), Times.Once);
    }

    [Fact]
    public async Task Login_InvalidPassword_ReturnsViewWithError()
    {
        using var context = CreateContext();
        var controller = CreateController(context, out _);

        var adminUser = new User
        {
            Email = "admin@test.com",
            PasswordHash = PasswordHasher.Hash("password123"),
            Role = "Admin"
        };
        context.Users.Add(adminUser);
        await context.SaveChangesAsync();

        var result = await controller.Login("admin@test.com", "wrongpassword");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        Assert.Equal("Email hoặc mật khẩu không đúng.", controller.ViewBag.Error);
    }

    [Fact]
    public async Task Login_UnverifiedOwner_ReturnsViewWithError()
    {
        using var context = CreateContext();
        var controller = CreateController(context, out _);

        var ownerUser = new User
        {
            Email = "owner@test.com",
            PasswordHash = PasswordHasher.Hash("password123"),
            Role = "poi_owner",
            IsVerified = false
        };
        context.Users.Add(ownerUser);
        await context.SaveChangesAsync();

        var result = await controller.Login("owner@test.com", "password123");

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("Index", view.ViewName);
        Assert.Equal("Tài khoản chủ quán đang chờ duyệt. Vui lòng đợi admin xác nhận.", controller.ViewBag.Error);
    }

    private static HomeController CreateController(VKStreetFoodDbContext context, out Mock<ISession> sessionMock)
    {
        var config = new ConfigurationBuilder().Build();

        sessionMock = new Mock<ISession>();
        var httpContext = new DefaultHttpContext();
        httpContext.Session = sessionMock.Object;

        var controller = new HomeController(
            new Repository<User>(context),
            new Repository<PointOfInterest>(context),
            new Repository<Vendor>(context),
            new Repository<PoiOwnerRegistration>(context),
            new UnitOfWork(context),
            config,
            NullLogger<HomeController>.Instance
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            },
            TempData = new Mock<ITempDataDictionary>().Object
        };

        return controller;
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new VKStreetFoodDbContext(options);
    }
}
