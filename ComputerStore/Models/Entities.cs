using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComputerStore.Models;

/// <summary>Hằng role dùng chung thay cho chuỗi tự do (B-11).</summary>
public static class Roles
{
    public const string Admin = "Admin";
    public const string Customer = "Customer";
}

public enum OrderStatus
{
    Pending = 0,    // Chờ xác nhận
    Confirmed = 1,  // Đã xác nhận
    Shipping = 2,   // Đang giao
    Completed = 3,  // Hoàn thành
    Cancelled = 4   // Đã hủy
}

// Lớp cơ sở dùng chung cho: Loại, Nhóm, Thương hiệu, Hình thức thanh toán
public abstract class LookupEntity
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên"), StringLength(100)]
    [Display(Name = "Tên")]
    public string Name { get; set; }

    [StringLength(500)]
    [Display(Name = "Mô tả")]
    public string Description { get; set; }
}

public class Category : LookupEntity            // Loại linh kiện: CPU, RAM, VGA...
{
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class ProductGroup : LookupEntity        // Nhóm linh kiện
{
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class Brand : LookupEntity               // Thương hiệu
{
    public ICollection<Product> Products { get; set; } = new List<Product>();
}

public class PaymentMethod : LookupEntity       // Hình thức thanh toán
{
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class Product                            // Linh kiện
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tên linh kiện"), StringLength(200)]
    [Display(Name = "Tên linh kiện")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mã SKU"), StringLength(50)]
    [Display(Name = "Mã SKU")]
    public string Sku { get; set; }

    [Display(Name = "Mô tả")]
    public string Description { get; set; }

    [Display(Name = "Thông số kỹ thuật")]
    public string Specifications { get; set; }

    [Range(0, 1000000000, ErrorMessage = "Giá không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Giá bán (₫)")]
    public decimal Price { get; set; }

    // Giá 0 được chấp nhận và hiểu là linh kiện tặng kèm (xem README §5).
    [Range(0, 1000000000, ErrorMessage = "Giá gốc không hợp lệ")]
    [Column(TypeName = "decimal(18,0)")]
    [Display(Name = "Giá gốc (₫)")]
    public decimal? OldPrice { get; set; }

    // Phần trăm giảm giá, chỉ tính khi có giá gốc lớn hơn giá bán
    [NotMapped]
    public int DiscountPercent => OldPrice.HasValue && OldPrice.Value > Price
        ? (int)Math.Round((1 - Price / OldPrice.Value) * 100)
        : 0;

    [Range(0, int.MaxValue, ErrorMessage = "Tồn kho không hợp lệ")]
    [Display(Name = "Tồn kho")]
    public int Stock { get; set; }

    [Range(0, 120)]
    [Display(Name = "Bảo hành (tháng)")]
    public int WarrantyMonths { get; set; } = 12;

    [StringLength(300)]
    public string ImageUrl { get; set; }

    [Display(Name = "Đang bán")]
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Loại linh kiện")]
    public int CategoryId { get; set; }
    public Category Category { get; set; }

    [Display(Name = "Nhóm linh kiện")]
    public int GroupId { get; set; }
    public ProductGroup Group { get; set; }

    [Display(Name = "Thương hiệu")]
    public int BrandId { get; set; }
    public Brand Brand { get; set; }
}

public class Customer
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ"), StringLength(100)]
    [Display(Name = "Email")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; }

    [StringLength(300)]
    [Display(Name = "Địa chỉ")]
    public string Address { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}

public class AppUser                            // Tài khoản đăng nhập
{
    public int Id { get; set; }

    [Required, StringLength(50)]
    public string Username { get; set; }

    [Required]
    public string PasswordHash { get; set; }

    [Required, StringLength(20)]
    public string Role { get; set; }            // Dùng hằng Roles.Admin / Roles.Customer (B-11)

    [StringLength(100)]
    public string DisplayName { get; set; }

    public int? CustomerId { get; set; }
    public Customer Customer { get; set; }

    // Chống brute-force (A-9): đếm số lần đăng nhập sai liên tiếp và thời điểm hết khóa.
    public int FailedLoginAttempts { get; set; }
    public DateTime? LockoutUntil { get; set; }
}

public class Order
{
    public int Id { get; set; }

    public int CustomerId { get; set; }
    public Customer Customer { get; set; }

    public DateTime OrderDate { get; set; } = DateTime.Now;
    public OrderStatus Status { get; set; } = OrderStatus.Pending;

    public int PaymentMethodId { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public bool IsPaid { get; set; }

    [Required, StringLength(100)]
    public string ReceiverName { get; set; }

    [Required, StringLength(20)]
    public string Phone { get; set; }

    [Required, StringLength(300)]
    public string ShippingAddress { get; set; }

    [StringLength(500)]
    public string Note { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal Total { get; set; }

    // Vòng 2: phí ship + giảm giá của đơn (đã tính lại phía server lúc checkout).
    // Total = tạm tính (tổng các dòng) + ShippingFee - DiscountAmount.
    [Column(TypeName = "decimal(18,0)")]
    public decimal ShippingFee { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal DiscountAmount { get; set; }

    [StringLength(50)]
    public string VoucherCode { get; set; }

    public ICollection<OrderDetail> Details { get; set; } = new List<OrderDetail>();
}

public class OrderDetail
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order Order { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal UnitPrice { get; set; }
}

public class StockReceipt                       // Phiếu nhập kho
{
    public int Id { get; set; }
    public DateTime ReceiptDate { get; set; } = DateTime.Now;

    [StringLength(200)]
    public string Supplier { get; set; }

    [StringLength(500)]
    public string Note { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal Total { get; set; }

    public ICollection<StockReceiptDetail> Details { get; set; } = new List<StockReceiptDetail>();
}

public class StockReceiptDetail
{
    public int Id { get; set; }
    public int StockReceiptId { get; set; }
    public StockReceipt StockReceipt { get; set; }
    public int ProductId { get; set; }
    public Product Product { get; set; }
    public int Quantity { get; set; }

    [Column(TypeName = "decimal(18,0)")]
    public decimal UnitCost { get; set; }
}

/// <summary>Yêu cầu gọi lại từ nút "Để lại số, chờ gọi lại" (A-4).</summary>
public class CallbackRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string Name { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; }

    [StringLength(500)]
    [Display(Name = "Nội dung cần tư vấn")]
    public string Note { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.Now;

    [Display(Name = "Đã xử lý")]
    public bool IsHandled { get; set; }
}

/// <summary>Bài tin tức / hướng dẫn chọn linh kiện (A-5).</summary>
public class NewsPost
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập tiêu đề"), StringLength(200)]
    [Display(Name = "Tiêu đề")]
    public string Title { get; set; }

    [StringLength(500)]
    [Display(Name = "Tóm tắt")]
    public string Summary { get; set; }

    [Display(Name = "Nội dung")]
    public string Content { get; set; }

    [StringLength(300)]
    [Display(Name = "Ảnh minh họa (URL)")]
    public string ImageUrl { get; set; }

    public DateTime PublishedAt { get; set; } = DateTime.Now;

    [Display(Name = "Hiển thị")]
    public bool IsPublished { get; set; } = true;
}

public class CartLine                           // Dòng giỏ hàng (lưu trong Session)
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
}
