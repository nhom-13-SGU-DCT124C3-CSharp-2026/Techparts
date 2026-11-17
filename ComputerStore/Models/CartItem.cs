using System.ComponentModel.DataAnnotations;

namespace ComputerStore.Models;

/// <summary>
/// Một dòng trong giỏ hàng bền của khách đã đăng nhập (vòng 2).
/// Khác với <see cref="CartLine"/> (Models/Entities.cs) chỉ là DTO lưu trong
/// Session cho khách vãng lai, <see cref="CartItem"/> là entity lưu trong
/// bảng CartItems nên giỏ không mất khi hết session hay đăng xuất.
/// Mỗi khách chỉ có tối đa một dòng cho mỗi sản phẩm (cặp CustomerId +
/// ProductId là duy nhất về mặt logic, xem CartService — không thêm unique
/// index vào DB trong vòng này để không sửa OnModelCreating).
/// </summary>
public class CartItem
{
    public int Id { get; set; }

    /// <summary>Khách sở hữu giỏ (FK tới Customer).</summary>
    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    /// <summary>Sản phẩm trong giỏ (FK tới Product).</summary>
    public int ProductId { get; set; }
    public Product Product { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "Số lượng không hợp lệ")]
    public int Quantity { get; set; }

    /// <summary>Lần cuối dòng giỏ được thêm/sửa/gộp (UTC).</summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
