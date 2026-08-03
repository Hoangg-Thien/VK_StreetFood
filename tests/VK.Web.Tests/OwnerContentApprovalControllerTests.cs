using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;
using VK.Web.Controllers;

namespace VK.Web.Tests;

public class OwnerContentApprovalControllerTests
{
    [Fact]
    public async Task Approve_WhenValidPendingCreateRequest_AppliesContentAndSetsStatusApproved()
    {
        using var context = CreateContext();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        var controller = CreateController(context, tempData);

        var poi = new PointOfInterest
        {
            Name = "Banh Mi Stall",
            Description = "Crispy banh mi",
            Address = "123 Street",
            IsActive = true
        };
        context.PointsOfInterest.Add(poi);
        await context.SaveChangesAsync();

        var request = new PoiContentChangeRequest
        {
            PointOfInterestId = poi.Id,
            RequestType = "audio",
            ActionType = "create",
            LanguageCode = "en",
            TextContent = "Best crispy banh mi in town",
            Status = "pending"
        };
        context.PoiContentChangeRequests.Add(request);
        await context.SaveChangesAsync();

        var result = await controller.Approve(request.Id, "Looks great!");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Đã duyệt và áp dụng thay đổi nội dung.", tempData["Success"]);

        var updatedRequest = await context.PoiContentChangeRequests
            .Include(r => r.AudioContent)
            .FirstAsync(r => r.Id == request.Id);

        Assert.Equal("approved", updatedRequest.Status);
        Assert.Equal("Looks great!", updatedRequest.ReviewNote);
        Assert.NotNull(updatedRequest.ReviewedAt);
        Assert.NotNull(updatedRequest.AudioContent);
        Assert.Equal("Best crispy banh mi in town", updatedRequest.AudioContent.TextContent);
        Assert.Equal("en", updatedRequest.AudioContent.LanguageCode);
    }

    [Fact]
    public async Task Approve_WhenTransactionThrows_RollsBackChangesAndReturnsError()
    {
        using var context = CreateContext();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());

        var poi = new PointOfInterest
        {
            Name = "Banh Mi Stall",
            Description = "Crispy banh mi",
            Address = "123 Street",
            IsActive = true
        };
        context.PointsOfInterest.Add(poi);

        var request = new PoiContentChangeRequest
        {
            PointOfInterestId = 1,
            RequestType = "audio",
            ActionType = "create",
            LanguageCode = "en",
            TextContent = "Crispy bread",
            Status = "pending"
        };
        context.PoiContentChangeRequests.Add(request);
        await context.SaveChangesAsync();

        // Create a mock UnitOfWork that simulates a failure during ExecuteInTransactionAsync
        var mockUow = new Mock<IUnitOfWork>();
        mockUow.Setup(u => u.ExecuteInTransactionAsync(It.IsAny<Func<Task>>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Simulated database failure during approval"));

        var controller = new OwnerContentApprovalController(
            new Repository<PoiContentChangeRequest>(context),
            new Repository<AudioContent>(context),
            new Repository<PointOfInterest>(context),
            mockUow.Object,
            NullLogger<OwnerContentApprovalController>.Instance)
        {
            TempData = tempData
        };

        var result = await controller.Approve(request.Id, "Note");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Không thể duyệt yêu cầu.", tempData["Error"]);

        // Verify request remains pending in database
        var unChangedRequest = await context.PoiContentChangeRequests.FirstAsync(r => r.Id == request.Id);
        Assert.Equal("pending", unChangedRequest.Status);
        Assert.Null(unChangedRequest.ReviewedAt);
    }

    [Fact]
    public async Task Reject_WhenPendingRequest_SetsStatusToRejected()
    {
        using var context = CreateContext();
        var tempData = new TempDataDictionary(new DefaultHttpContext(), Mock.Of<ITempDataProvider>());
        var controller = CreateController(context, tempData);

        var request = new PoiContentChangeRequest
        {
            PointOfInterestId = 1,
            RequestType = "audio",
            ActionType = "create",
            LanguageCode = "en",
            TextContent = "Inaccurate info",
            Status = "pending"
        };
        context.PoiContentChangeRequests.Add(request);
        await context.SaveChangesAsync();

        var result = await controller.Reject(request.Id, "Content not accurate");

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.Equal("Đã từ chối yêu cầu.", tempData["Success"]);

        var updatedRequest = await context.PoiContentChangeRequests.FirstAsync(r => r.Id == request.Id);
        Assert.Equal("rejected", updatedRequest.Status);
        Assert.Equal("Content not accurate", updatedRequest.ReviewNote);
    }

    private static OwnerContentApprovalController CreateController(VKStreetFoodDbContext context, ITempDataDictionary tempData)
    {
        return new OwnerContentApprovalController(
            new Repository<PoiContentChangeRequest>(context),
            new Repository<AudioContent>(context),
            new Repository<PointOfInterest>(context),
            new UnitOfWork(context),
            NullLogger<OwnerContentApprovalController>.Instance)
        {
            TempData = tempData
        };
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new VKStreetFoodDbContext(options);
    }
}
