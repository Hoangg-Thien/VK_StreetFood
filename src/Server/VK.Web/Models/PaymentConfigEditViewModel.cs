namespace VK.Web.Models;

public class PaymentConfigEditViewModel
{
    public decimal DefaultAmountVnd { get; set; }
    public string DeepLinkName { get; set; } = "pay";
    public int QrTtlMinutes { get; set; } = 15;

    public string DeepLinkPreview => $"vkstreetfood://{DeepLinkName}";
}
