using VK.Shared.DTOs;

namespace VK.API.Services.AppServices;

public interface IPaymentAppService
{
    Task<QrPaymentConfigDto> GetQrPaymentConfigAsync();
}
