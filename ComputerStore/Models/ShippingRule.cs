using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerStore.Models;

/// <summary>
/// Quy tắc phí vận chuyển (vòng 2).
/// Khi tính phí, chỉ xét các quy tắc đang áp dụng (IsActive), lấy quy tắc có
/// SortOrder nhỏ nhất; đơn có tạm tính &gt;= FreeFromAmount thì miễn phí,
/// ngược lại tính Fee. Nếu không có quy tắc nào đang áp dụng thì phí = 0.
/// </summary>
public class ShippingRule
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên quy tắc"), StringLength(100)]
    [Display(Name = "Tên quy tắc")]
    public string Name { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Ngưỡng miễn phí không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Miễn phí từ (₫)")]
    public decimal FreeFromAmount { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Phí ship không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Phí ship (₫)")]
    public decimal Fee { get; set; }

    [Display(Name = "Đang áp dụng")]
    public bool IsActive { get; set; } = true;

    [Display(Name = "Thứ tự")]
    public int SortOrder { get; set; }
}
