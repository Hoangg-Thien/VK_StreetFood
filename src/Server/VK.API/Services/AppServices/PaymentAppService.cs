using Microsoft.EntityFrameworkCore;
using VK.Core.Entities;
using VK.Core.Interfaces;
using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public class PaymentAppService : IPaymentAppService
{
    private readonly IRepository<QrPaymentConfig> _configRepository;
    private readonly IUnitOfWork _unitOfWork;

    public PaymentAppService(IRepository<QrPaymentConfig> configRepository, IUnitOfWork unitOfWork)
    {
        _configRepository = configRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<QrPaymentConfigDto> GetQrPaymentConfigAsync()
    {
        var config = await _configRepository.Query()
            .OrderBy(c => c.Id)
            .FirstOrDefaultAsync();

        if (config == null)
        {
            config = new QrPaymentConfig
            {
                DefaultAmountVnd = 0,
                DeepLinkName = "pay",
                QrTtlMinutes = 15
            };

            await _configRepository.AddAsync(config);
            await _unitOfWork.SaveChangesAsync();
        }

        return new QrPaymentConfigDto
        {
            DefaultAmountVnd = config.DefaultAmountVnd,
            DeepLinkName = config.DeepLinkName,
            QrTtlMinutes = config.QrTtlMinutes
        };
    }
}
