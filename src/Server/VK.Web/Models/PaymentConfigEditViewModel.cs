namespace VK.Web.Models;

public class PaymentConfigEditViewModel
{
    public decimal DefaultAmountVnd { get; set; }
    public string DeepLinkName { get; set; } = "pay";
    public int QrTtlMinutes { get; set; } = 15;
    public string SelectedStatus { get; set; } = string.Empty;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public List<PaymentHistoryItemViewModel> PaymentHistory { get; set; } = new();

    public string DeepLinkPreview => $"vkstreetfood://{DeepLinkName}";
}

public class PaymentHistoryItemViewModel
{
    public DateTime OccurredAt { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusCode { get; set; } = string.Empty;
    public string PoiName { get; set; } = string.Empty;
}
