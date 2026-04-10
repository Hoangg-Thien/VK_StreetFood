using Microsoft.AspNetCore.Mvc;

namespace VK.API.Services.AppServices;

public interface IPaymentAppService
{
    Task<IActionResult> GetQrPaymentConfigAsync();
}
