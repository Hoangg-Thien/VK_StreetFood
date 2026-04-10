using System.Text.Json.Serialization;

namespace VK.Mobile.Models;

public class QrPaymentConfigModel
{
    [JsonPropertyName("defaultAmountVnd")]
    public decimal DefaultAmountVnd { get; set; }

    [JsonPropertyName("deepLinkName")]
    public string DeepLinkName { get; set; } = "pay";

    [JsonPropertyName("qrTtlMinutes")]
    public int QrTtlMinutes { get; set; } = 15;
}
