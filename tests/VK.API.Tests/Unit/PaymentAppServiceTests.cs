using Microsoft.EntityFrameworkCore;
using VK.API.Services.AppServices;
using VK.Core.Entities;
using VK.Infrastructure.Data;
using VK.Infrastructure.Repositories;

namespace VK.API.Tests.Unit;

public class PaymentAppServiceTests
{
    [Fact]
    public async Task GetQrPaymentConfigAsync_CreatesDefaultConfig_WhenNoneExists()
    {
        using var context = CreateContext();
        var service = new PaymentAppService(
            new Repository<QrPaymentConfig>(context),
            new UnitOfWork(context));

        var config = await service.GetQrPaymentConfigAsync();

        Assert.NotNull(config);
        Assert.Equal(0, config.DefaultAmountVnd);
        Assert.Equal("pay", config.DeepLinkName);
        Assert.Equal(15, config.QrTtlMinutes);
        Assert.Equal(1, await context.QrPaymentConfigs.CountAsync());
    }

    [Fact]
    public async Task GetQrPaymentConfigAsync_ReturnsExistingConfig_WhenAlreadyExists()
    {
        using var context = CreateContext();
        context.QrPaymentConfigs.Add(new QrPaymentConfig
        {
            DefaultAmountVnd = 50000,
            DeepLinkName = "custom_pay",
            QrTtlMinutes = 30
        });
        await context.SaveChangesAsync();

        var service = new PaymentAppService(
            new Repository<QrPaymentConfig>(context),
            new UnitOfWork(context));

        var config = await service.GetQrPaymentConfigAsync();

        Assert.NotNull(config);
        Assert.Equal(50000, config.DefaultAmountVnd);
        Assert.Equal("custom_pay", config.DeepLinkName);
        Assert.Equal(30, config.QrTtlMinutes);
        Assert.Equal(1, await context.QrPaymentConfigs.CountAsync());
    }

    private static VKStreetFoodDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<VKStreetFoodDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options;
        return new VKStreetFoodDbContext(options);
    }
}
