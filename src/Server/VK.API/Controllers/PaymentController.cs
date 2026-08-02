using Microsoft.AspNetCore.Mvc;
using VK.API.Services.AppServices;

namespace VK.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentController : ControllerBase
{
    private readonly IPaymentAppService _paymentAppService;

    public PaymentController(IPaymentAppService paymentAppService)
    {
        _paymentAppService = paymentAppService;
    }

    [HttpGet("qr-config")]
    public async Task<IActionResult> GetQrConfig()
        => Ok(await _paymentAppService.GetQrPaymentConfigAsync());
}
