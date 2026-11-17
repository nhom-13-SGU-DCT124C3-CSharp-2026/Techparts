using System.ComponentModel.DataAnnotations;

namespace ComputerStore.Models;

/// <summary>
/// ViewModel sửa phiếu nhập kho (B-13). File mới, không sửa ViewModels.cs.
/// </summary>
public class StockReceiptEditVm
{
    public int Id { get; set; }

    public DateTime ReceiptDate { get; set; }

    [StringLength(200)]
    [Display(Name = "Nhà cung cấp")]
    public string Supplier { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string Note { get; set; }

    /// <summary>
    /// Các dòng của phiếu. Dòng đã có: Id = Id của StockReceiptDetail, không
    /// được đổi sản phẩm (chỉ sửa số lượng / đơn giá) để tránh nhầm lẫn khi
    /// đảo ngược tồn kho. Dòng mới thêm: Id = 0, chọn sản phẩm chưa có trong
    /// phiếu. Dòng bị xóa khỏi form = xóa khỏi phiếu.
    /// </summary>
    public List<StockReceiptEditLineVm> Lines { get; set; } = new();
}

public class StockReceiptEditLineVm
{
    /// <summary>Id của StockReceiptDetail; 0 = dòng mới thêm.</summary>
    public int Id { get; set; }

    public int ProductId { get; set; }

    /// <summary>Tên hiển thị (không bind từ form khi sửa — nạp lại từ DB).</summary>
    public string ProductName { get; set; }

    public string Sku { get; set; }

    public int Quantity { get; set; }

    public decimal UnitCost { get; set; }
}
