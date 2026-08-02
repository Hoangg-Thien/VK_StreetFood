using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VK.API.Controllers;
using VK.API.Services;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Shared.DTOs;

namespace VK.API.Tests.Unit;

public class AudioControllerTests
{
    [Fact]
    public async Task GetOrGenerateTts_ReturnsAudioFileUrl_WhenAlreadyGenerated()
    {
        using var context = CreateContext();
        var ttsMock = new Mock<ITtsGenerationService>();
        var audioTaskMock = new Mock<IAudioTaskManager>();

        var controller = new AudioController(
            context,
            ttsMock.Object,
            audioTaskMock.Object,
            NullLogger<AudioController>.Instance);

        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        var poi = new PointOfInterest
        {
            Name = "Sample POI",
            Description = "Sample Desc",
            IsActive = true
        };
        context.PointsOfInterest.Add(poi);

        var audioContent = new AudioContent
        {
            PointOfInterest = poi,
            LanguageCode = "en",
            TextContent = "Hello from TTS",
            AudioFileUrl = "/audio/sample.mp3",
            IsGenerated = true
        };
        context.AudioContents.Add(audioContent);
        await context.SaveChangesAsync();

        // Mock the task manager to return the existing URL
        audioTaskMock
            .Setup(x => x.GetOrGenerateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/audio/sample.mp3");

        var request = new OnDemandTtsRequest { PoiId = poi.Id, LanguageCode = "en" };
        var result = await controller.GetOrGenerateTts(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value as dynamic;
        Assert.NotNull(value);

        var type = value.GetType();
        var urlProp = type.GetProperty("audioFileUrl");
        Assert.NotNull(urlProp);
        Assert.Equal("http://localhost/audio/sample.mp3", urlProp.GetValue(value).ToString());

        audioTaskMock.Verify(x => x.GetOrGenerateAsync(poi.Id, "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetOrGenerateTts_CallsTaskManager_WhenNotGenerated()
    {
        using var context = CreateContext();
        var ttsMock = new Mock<ITtsGenerationService>();
        var audioTaskMock = new Mock<IAudioTaskManager>();

        // Mock the task manager to return a generated URL
        audioTaskMock
            .Setup(x => x.GetOrGenerateAsync(It.IsAny<int>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync("/audio/generated.mp3");

        var controller = new AudioController(
            context,
            ttsMock.Object,
            audioTaskMock.Object,
            NullLogger<AudioController>.Instance);

        // Setup a mock HttpContext to avoid null reference on HttpContext.Request
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Scheme = "http";
        httpContext.Request.Host = new HostString("localhost");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        var poi = new PointOfInterest
        {
            Name = "Sample POI",
            Description = "Sample Desc",
            IsActive = true
        };
        context.PointsOfInterest.Add(poi);

        var audioContent = new AudioContent
        {
            PointOfInterest = poi,
            LanguageCode = "en",
            TextContent = "Pending generation",
            IsGenerated = false
        };
        context.AudioContents.Add(audioContent);
        await context.SaveChangesAsync();

        var request = new OnDemandTtsRequest { PoiId = poi.Id, LanguageCode = "en" };
        var result = await controller.GetOrGenerateTts(request, CancellationToken.None);

        var ok = Assert.IsType<OkObjectResult>(result);
        var value = ok.Value as dynamic;

        var type = value.GetType();
        var urlProp = type.GetProperty("audioFileUrl");
        Assert.NotNull(urlProp);
        Assert.Equal("http://localhost/audio/generated.mp3", urlProp.GetValue(value).ToString());

        // Verify task manager was called
        audioTaskMock.Verify(x => x.GetOrGenerateAsync(poi.Id, "en", It.IsAny<CancellationToken>()), Times.Once);
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new VKStreetFoodDbContext(options);
    }
}
