using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Web.Controllers;
using System.Text;

namespace VK.Web.Tests;

public class OwnerControllerTests
{
    [Fact]
    public async Task Index_ValidSession_ReturnsViewWithViewBagData()
    {
        using var context = CreateContext();
        
        var poi = new PointOfInterest { Name = "My POI", IsActive = true };
        context.PointsOfInterest.Add(poi);
        
        var vendor = new Vendor { PointOfInterest = poi, IsActive = true };
        context.Vendors.Add(vendor);
        await context.SaveChangesAsync();

        var user = new User { Email = "owner@test.com", Role = "poi_owner", VendorId = vendor.Id, IsVerified = true };
        context.Users.Add(user);
        
        var audio = new AudioContent { PointOfInterest = poi, LanguageCode = "en", TextContent = "Hello", CreatedAt = DateTime.UtcNow };
        context.AudioContents.Add(audio);

        await context.SaveChangesAsync();

        var controller = CreateController(context, vendor.Id, "owner@test.com");

        var result = await controller.Index();

        var view = Assert.IsType<ViewResult>(result);
        Assert.Equal("OwnerPage", view.ViewName);
        
        var audiosList = Assert.IsAssignableFrom<IEnumerable<AudioContent>>(view.ViewData["Audios"]);
        Assert.Single(audiosList);
        
        var poiObj = Assert.IsType<PointOfInterest>(view.ViewData["Poi"]);
        Assert.Equal("My POI", poiObj.Name);
    }

    [Fact]
    public async Task Index_InvalidSession_RedirectsToHome()
    {
        using var context = CreateContext();
        var controller = CreateController(context, null, null);

        var result = await controller.Index();

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Home", redirect.ControllerName);
        Assert.Equal("Index", redirect.ActionName);
    }

    private static OwnerController CreateController(VKStreetFoodDbContext context, int? vendorId, string? email)
    {
        var httpContext = new DefaultHttpContext();
        var session = new FakeSession();
        
        if (email != null)
        {
            session.SetString("UserLoggedIn", "true");
            session.SetString("UserRole", "poi_owner");
            session.SetString("UserEmail", email);
        }
        if (vendorId.HasValue)
        {
            session.SetInt32("VendorId", vendorId.Value);
        }
        
        httpContext.Session = session;

        return new OwnerController(
            new Repository<AudioContent>(context),
            new Repository<PoiContentChangeRequest>(context),
            new Repository<User>(context),
            new Repository<Vendor>(context),
            new UnitOfWork(context),
            NullLogger<OwnerController>.Instance
        )
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = httpContext
            }
        };
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new VKStreetFoodDbContext(options);
    }

    private class FakeSession : ISession
    {
        private readonly Dictionary<string, byte[]> _store = new();
        public bool IsAvailable => true;
        public string Id => "fake-session";
        public IEnumerable<string> Keys => _store.Keys;
        public void Clear() => _store.Clear();
        public Task CommitAsync(CancellationToken token = default) => Task.CompletedTask;
        public Task LoadAsync(CancellationToken token = default) => Task.CompletedTask;
        public void Remove(string key) => _store.Remove(key);
        public void Set(string key, byte[] value) => _store[key] = value;
        public bool TryGetValue(string key, out byte[] value) => _store.TryGetValue(key, out value!);
    }
}
