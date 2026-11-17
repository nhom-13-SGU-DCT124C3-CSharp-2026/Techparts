using System.ComponentModel.DataAnnotations;

namespace ComputerStore.Models;

public class LoginVm
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; }

    [Display(Name = "Ghi nhớ đăng nhập")]
    public bool RememberMe { get; set; }

    public string ReturnUrl { get; set; }
}

public class RegisterVm
{
    [Required(ErrorMessage = "Vui lòng nhập tên đăng nhập")]
    [StringLength(50, MinimumLength = 4, ErrorMessage = "Tên đăng nhập từ 4 đến 50 ký tự")]
    [Display(Name = "Tên đăng nhập")]
    public string Username { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập mật khẩu")]
    [StringLength(100, MinimumLength = PasswordRules.MinLength, ErrorMessage = PasswordRules.ErrorMessage)]
    [RegularExpression(PasswordRules.Pattern, ErrorMessage = PasswordRules.ErrorMessage)]
    [DataType(DataType.Password)]
    [Display(Name = "Mật khẩu")]
    public string Password { get; set; }

    [Compare("Password", ErrorMessage = "Mật khẩu xác nhận không khớp")]
    [DataType(DataType.Password)]
    [Display(Name = "Xác nhận mật khẩu")]
    public string ConfirmPassword { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập họ tên"), StringLength(100)]
    [Display(Name = "Họ và tên")]
    public string FullName { get; set; }

    [EmailAddress(ErrorMessage = "Email không hợp lệ")]
    [Display(Name = "Email")]
    public string Email { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; }

    [Display(Name = "Địa chỉ")]
    public string Address { get; set; }
}

public class CartItemVm
{
    public Product Product { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal => Product.Price * Quantity;
}

public class CheckoutVm
{
    [Required(ErrorMessage = "Vui lòng nhập tên người nhận"), StringLength(100)]
    [Display(Name = "Người nhận")]
    public string ReceiverName { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập số điện thoại")]
    [RegularExpression(@"^(\+84|0)\d{9,10}$", ErrorMessage = "Số điện thoại không hợp lệ")]
    [Display(Name = "Số điện thoại")]
    public string Phone { get; set; }

    [Required(ErrorMessage = "Vui lòng nhập địa chỉ giao hàng"), StringLength(300)]
    [Display(Name = "Địa chỉ giao hàng")]
    public string ShippingAddress { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string Note { get; set; }

    [Display(Name = "Hình thức thanh toán")]
    public int PaymentMethodId { get; set; }

    // Vòng 2: mã voucher khách nhập + phí ship/giảm giá đã tính phía server.
    [Display(Name = "Mã giảm giá")]
    [StringLength(50)]
    public string VoucherCode { get; set; }

    public decimal ShippingFee { get; set; }
    public decimal DiscountAmount { get; set; }

    public List<CartItemVm> Items { get; set; } = new();
    public List<PaymentMethod> PaymentMethods { get; set; } = new();
    public decimal Total => Items.Sum(i => i.LineTotal);

    /// <summary>Tổng cuối khách phải trả = tạm tính + phí ship - giảm giá.</summary>
    public decimal GrandTotal => Total + ShippingFee - DiscountAmount;
}

public class PriceRangeVm
{
    public string Label { get; set; }
    public decimal? Min { get; set; }
    public decimal? Max { get; set; }
}

public class HomeSectionVm
{
    public string Title { get; set; }
    public string Icon { get; set; }
    public List<int> CategoryIds { get; set; } = new();
    // Section gộp nhiều loại dùng GroupId để link "Xem tất cả" cho đúng (A-6);
    // section chỉ có 1 loại thì GroupId = null và dùng CategoryIds.First().
    public int? GroupId { get; set; }
    public List<Product> Products { get; set; } = new();
    public int TotalCount { get; set; }
    public List<Brand> Brands { get; set; } = new();
    public List<PriceRangeVm> PriceRanges { get; set; } = new();
}

public class HomeIndexVm
{
    public List<Category> Categories { get; set; } = new();
    public List<ProductGroup> Groups { get; set; } = new();
    public List<Product> Discounted { get; set; } = new();
    public List<HomeSectionVm> Sections { get; set; } = new();
    public List<NewsPost> News { get; set; } = new();
}

public class ShopIndexVm
{
    public List<Product> Products { get; set; } = new();
    public List<Category> Categories { get; set; } = new();
    public List<ProductGroup> Groups { get; set; } = new();
    public List<Brand> Brands { get; set; } = new();
    public string Q { get; set; }
    public int? CategoryId { get; set; }
    public int? GroupId { get; set; }
    public int? BrandId { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string Sort { get; set; }
    public int Page { get; set; } = 1;
    public int TotalPages { get; set; } = 1;
    public int TotalItems { get; set; }
    // Lọc chỉ món đang giảm giá (OldPrice > Price) — dùng cho "Xem tất cả" mục giảm giá (B-6).
    public bool Discounted { get; set; }
}

public class StockItemVm
{
    public int ProductId { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
}

public class StockReceiptCreateVm
{
    [StringLength(200)]
    [Display(Name = "Nhà cung cấp")]
    public string Supplier { get; set; }

    [StringLength(500)]
    [Display(Name = "Ghi chú")]
    public string Note { get; set; }

    public List<StockItemVm> Items { get; set; } = new();
}

public class DashboardVm
{
    public int ProductCount { get; set; }
    public int CustomerCount { get; set; }
    public int OrderCount { get; set; }
    public int PendingOrders { get; set; }
    public decimal Revenue { get; set; }
    public List<Product> LowStock { get; set; } = new();
    public List<Order> RecentOrders { get; set; } = new();
}

/// <summary>ViewModel trang Build PC (A-5): chọn 1 linh kiện cho mỗi vị trí.</summary>
public class BuildPcVm
{
    public List<Product> Cpus { get; set; } = new();
    public List<Product> Mainboards { get; set; } = new();
    public List<Product> Rams { get; set; } = new();
    public List<Product> Vgas { get; set; } = new();
    public List<Product> Psus { get; set; } = new();
    public List<Product> Storages { get; set; } = new();
    public List<Product> Cases { get; set; } = new();

    public int? CpuId { get; set; }
    public int? MainboardId { get; set; }
    public int? RamId { get; set; }
    public int? VgaId { get; set; }
    public int? PsuId { get; set; }
    public int? StorageId { get; set; }
    public int? CaseId { get; set; }

    public decimal Total { get; set; }
    // Cảnh báo tương thích (socket CPU/mainboard, chuẩn RAM) và ghi chú giới hạn kiểm tra.
    public List<string> Warnings { get; set; } = new();
    public List<string> Notes { get; set; } = new();
    public bool HasSelection { get; set; }
}
