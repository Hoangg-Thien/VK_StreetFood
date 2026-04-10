using System.ComponentModel.DataAnnotations;

namespace VK.Core.Entities;

public class QrPaymentConfig : BaseEntity
{
    /// <summary>
    /// Muc gia mac dinh (VND) cho luong QR payment. Hien tai de 0 khi chua tich hop cong thanh toan that.
    /// </summary>
    public decimal DefaultAmountVnd { get; set; } = 0;

    /// <summary>
    /// Ten host deep link sau scheme vkstreetfood:// (vd: pay).
    /// </summary>
    [MaxLength(50)]
    public string DeepLinkName { get; set; } = "pay";

    /// <summary>
    /// Thoi gian hieu luc QR tinh theo phut.
    /// </summary>
    public int QrTtlMinutes { get; set; } = 15;
}
