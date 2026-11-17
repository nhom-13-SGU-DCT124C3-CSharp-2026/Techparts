using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerStore.Models;

public enum DiscountType
{
    Percent = 0, // Giảm theo % trên tạm tính (kẹp bởi MaxDiscountAmount nếu có)
    Fixed = 1    // Giảm số tiền cố định (không vượt tạm tính)
}

/// <summary>
/// Mã giảm giá (voucher, vòng 2).
/// Code là duy nhất — kiểm tra ở tầng ứng dụng (VoucherService + admin CRUD);
/// chưa có unique index ở DB (xem FIX_LOG_2_CHECKOUT.md).
/// Code được chuẩn hóa thành chữ hoa, không khoảng trắng khi lưu.
/// </summary>
public class Voucher
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã voucher"), StringLength(50)]
    [Display(Name = "Mã voucher")]
    public string Code { get; set; }

    [Display(Name = "Loại giảm")]
    public DiscountType DiscountType { get; set; } = DiscountType.Percent;

    // Percent: % giảm (0-100, kiểm tra ở admin CRUD). Fixed: số tiền giảm (₫).
    [Range(0, 1000000000, ErrorMessage = "Giá trị giảm không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Giá trị giảm")]
    public decimal DiscountValue { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Đơn tối thiểu không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Đơn tối thiểu (₫)")]
    public decimal MinOrderAmount { get; set; }

    // Chỉ có ý nghĩa với loại Percent; với Fixed thì để trống.
    [Range(0, 1000000000, ErrorMessage = "Giảm tối đa không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Giảm tối đa (₫)")]
    public decimal? MaxDiscountAmount { get; set; }

    [Display(Name = "Hiệu lực từ")]
    public DateTime ValidFrom { get; set; }

    [Display(Name = "Hiệu lực đến")]
    public DateTime ValidTo { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượt tối đa phải từ 1 trở lên")]
    [Display(Name = "Số lượt tối đa")]
    public int MaxUses { get; set; }

    [Display(Name = "Đã dùng")]
    public int UsedCount { get; set; }

    [Display(Name = "Đang áp dụng")]
    public bool IsActive { get; set; } = true;
}
