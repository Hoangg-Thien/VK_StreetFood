namespace VK.Shared.DTOs;

public class QrPaymentConfigDto
{
    public decimal DefaultAmountVnd { get; set; }
    public string DeepLinkName { get; set; } = "pay";
    public int QrTtlMinutes { get; set; }
}
