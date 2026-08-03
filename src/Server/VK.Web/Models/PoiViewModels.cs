using System.ComponentModel.DataAnnotations;

namespace VK.Web.Models;

/// <summary>
/// Form model for creating a new Point of Interest.
/// Prevents overposting / mass assignment by exposing only editable presentation fields.
/// </summary>
public class PoiCreateViewModel
{
    [Required(ErrorMessage = "Tên địa điểm không được để trống")]
    [StringLength(200, ErrorMessage = "Tên địa điểm không được vượt quá 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Địa chỉ không được để trống")]
    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string Address { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180")]
    public double Longitude { get; set; }

    public int? CategoryId { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; } = true;

    [Range(0, 1000, ErrorMessage = "Độ ưu tiên phải từ 0 đến 1000")]
    public int TriggerPriority { get; set; } = 50;

    [Range(1.0, 10000.0, ErrorMessage = "Bán kính kích hoạt phải từ 1m đến 10000m")]
    public double? TriggerRadiusMeters { get; set; }
}

/// <summary>
/// Form model for editing an existing Point of Interest.
/// Prevents overposting / mass assignment by exposing only editable presentation fields.
/// </summary>
public class PoiEditViewModel
{
    [Required(ErrorMessage = "ID địa điểm không được để trống")]
    [Range(1, int.MaxValue, ErrorMessage = "ID địa điểm không hợp lệ")]
    public int Id { get; set; }

    [Required(ErrorMessage = "Tên địa điểm không được để trống")]
    [StringLength(200, ErrorMessage = "Tên địa điểm không được vượt quá 200 ký tự")]
    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    [Required(ErrorMessage = "Địa chỉ không được để trống")]
    [StringLength(500, ErrorMessage = "Địa chỉ không được vượt quá 500 ký tự")]
    public string Address { get; set; } = string.Empty;

    [Range(-90.0, 90.0, ErrorMessage = "Vĩ độ phải nằm trong khoảng -90 đến 90")]
    public double Latitude { get; set; }

    [Range(-180.0, 180.0, ErrorMessage = "Kinh độ phải nằm trong khoảng -180 đến 180")]
    public double Longitude { get; set; }

    public int? CategoryId { get; set; }

    public string? ImageUrl { get; set; }

    public bool IsActive { get; set; }

    [Range(0, 1000, ErrorMessage = "Độ ưu tiên phải từ 0 đến 1000")]
    public int TriggerPriority { get; set; } = 50;

    [Range(1.0, 10000.0, ErrorMessage = "Bán kính kích hoạt phải từ 1m đến 10000m")]
    public double? TriggerRadiusMeters { get; set; }
}
