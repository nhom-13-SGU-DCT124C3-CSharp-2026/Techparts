using ComputerStore.Data;
using ComputerStore.Helpers;
using ComputerStore.Models;
using ComputerStore.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Controllers;

public class CartController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<CartController> _logger;
    private readonly CartService _cart;
    private readonly OrderPricingService _pricing;
    private readonly VoucherService _vouchers;

    // Vòng 2: đăng ký DI ở Program.cs — CartService (giỏ bền), OrderPricingService,
    // VoucherService. KHÔNG có cổng thanh toán (quyết định của nhóm 2026-11-15:
    // bỏ hẳn thanh toán online, chỉ dùng COD/chuyển khoản).
    public CartController(AppDbContext db, ILogger<CartController> logger, CartService cart,
        OrderPricingService pricing, VoucherService vouchers)
    {
        _db = db;
        _logger = logger;
        _cart = cart;
        _pricing = pricing;
        _vouchers = vouchers;
    }

    // Giỏ hàng bền (vòng 2): khách đã đăng nhập đọc/ghi giỏ từ DB (CartService),
    // khách vãng lai giữ nguyên giỏ session như cũ. Lần đầu sau khi đăng nhập
    // mà khách chạm vào giỏ, giỏ session (nếu có) được gộp lazy vào DB rồi
    // xóa session — xem CartService (không chặn login, không sửa AccountController).
    private async Task MergeSessionIfAnyAsync(int customerId)
    {
        var sessionLines = HttpContext.Session.GetCart();
        if (sessionLines.Count == 0) return;
        await _cart.MergeSessionCartAsync(customerId, sessionLines);
        HttpContext.Session.ClearCart();
    }

    /// <summary>
    /// Chỉ liệt kê COD và chuyển khoản ngân hàng (quyết định của nhóm ngày 2026-11-15:
    /// KHÔNG làm thanh toán online — không có cổng thanh toán, không có VNPay).
    /// "Thẻ tín dụng / ghi nợ" và "Ví điện tử MoMo" vẫn giữ tên trong DB (seed từ Lần 1)
    /// nhưng KHÔNG hiện ở checkout vì không có hình thức thanh toán thật đằng sau.
    /// </summary>
    private static readonly string[] AllowedPaymentMethodNames =
    {
        "Thanh toán khi nhận hàng (COD)",
        "Chuyển khoản ngân hàng"
    };

    private async Task<List<PaymentMethod>> GetAllowedPaymentMethodsAsync()
    {
        var methods = await _db.PaymentMethods.OrderBy(p => p.Id).ToListAsync();
        return methods.Where(m => AllowedPaymentMethodNames.Contains(m.Name))
            .OrderBy(m => m.Id).ToList();
    }

    private static bool IsAllowedPaymentMethod(PaymentMethod method) =>
        method != null && AllowedPaymentMethodNames.Contains(method.Name);

    /// <summary>
    /// Hướng dẫn chuyển khoản hiển thị ở trang đặt hàng thành công.
    /// Nhóm không làm thanh toán online nên không có số tài khoản trong code:
    /// khách liên hệ hotline 1900 0000 để nhận thông tin chuyển khoản.
    /// </summary>
    public static string[] BankTransferInstructionLines(Order order) => new[]
    {
        "Vui lòng liên hệ hotline 1900 0000 để nhận thông tin chuyển khoản.",
        $"Nội dung chuyển khoản: TECHPARTS-{order.Id}",
        $"Số tiền cần chuyển: {order.Total.Money()}"
    };

    /// <summary>Tính phí ship/giảm giá/tổng cuối cho giỏ hiện tại (tính lại phía server).</summary>
    private async Task<PricingResult> PriceItemsAsync(List<CartItemVm> items, Voucher voucher)
    {
        var subtotal = items.Sum(i => i.LineTotal);
        var rules = await _db.ShippingRules.Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder).ToListAsync();
        return _pricing.Calculate(subtotal, rules, voucher);
    }

    // Dựng danh sách sản phẩm trong giỏ, tự điều chỉnh theo tồn kho hiện tại.
    // Khác với trước đây, mọi dòng bị bỏ/kẹp đều được báo cho người dùng (B-7)
    // qua TempData["Warning"] thay vì để giỏ tự đổi lặng lẽ.
    private async Task<List<CartItemVm>> BuildItems()
    {
        var cid = User.CustomerId();
        var sessionLines = HttpContext.Session.GetCart();
        // GetItemsAsync tự gộp lazy giỏ session vào DB khi khách đã đăng nhập.
        var load = await _cart.GetItemsAsync(cid, sessionLines);
        if (cid.HasValue && sessionLines.Count > 0)
            HttpContext.Session.ClearCart(); // đã gộp xong, không gộp lại lần sau
        var cart = load.Lines;

        var ids = cart.Select(c => c.ProductId).ToList();
        var products = await _db.Products.Include(p => p.Brand)
            .Where(p => ids.Contains(p.Id) && p.IsActive).ToListAsync();

        var items = new List<CartItemVm>();
        var notices = new List<string>(load.MergeNotices);
        foreach (var line in cart)
        {
            var p = products.FirstOrDefault(x => x.Id == line.ProductId);
            if (p == null)
            {
                notices.Add("Một sản phẩm trong giỏ đã ngừng bán hoặc không còn tồn tại nên đã được bỏ khỏi giỏ.");
                continue;
            }
            if (p.Stock <= 0)
            {
                notices.Add($"\"{p.Name}\" đã hết hàng nên đã được bỏ khỏi giỏ.");
                continue;
            }
            var qty = Math.Min(line.Quantity, p.Stock);
            if (qty < line.Quantity)
                notices.Add($"\"{p.Name}\" chỉ còn {p.Stock} chiếc nên số lượng đã giảm từ {line.Quantity} xuống {qty}.");
            items.Add(new CartItemVm { Product = p, Quantity = qty });
        }

        if (notices.Any())
            TempData["Warning"] = string.Join(" ", notices.Distinct());

        // Lưu lại giỏ đã điều chỉnh theo tồn kho: DB nếu đã đăng nhập,
        // session nếu vãng lai (hành vi cũ).
        var adjusted = items.Select(i => new CartLine
            { ProductId = i.Product.Id, Quantity = i.Quantity }).ToList();
        if (cid.HasValue)
            await _cart.SaveLinesAsync(cid.Value, adjusted);
        else
            HttpContext.Session.SaveCart(adjusted);
        return items;
    }

    public async Task<IActionResult> Index() => View(await BuildItems());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Add(int id, int qty = 1, string returnUrl = null)
    {
        var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product == null) return NotFound();

        if (product.Stock <= 0)
        {
            TempData["Error"] = "Sản phẩm đã hết hàng.";
        }
        else
        {
            var cid = User.CustomerId();
            if (cid.HasValue)
            {
                await MergeSessionIfAnyAsync(cid.Value);
                await _cart.AddAsync(cid.Value, id, qty);
                TempData["Success"] = $"Đã thêm \"{product.Name}\" vào giỏ hàng.";
            }
            else
            {
                var cart = HttpContext.Session.GetCart();
                var line = cart.FirstOrDefault(c => c.ProductId == id);
                if (line == null) cart.Add(new CartLine { ProductId = id, Quantity = Math.Clamp(qty, 1, product.Stock) });
                else line.Quantity = Math.Min(line.Quantity + Math.Max(qty, 1), product.Stock);
                HttpContext.Session.SaveCart(cart);
                TempData["Success"] = $"Đã thêm \"{product.Name}\" vào giỏ hàng.";
            }
        }

        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)) return Redirect(returnUrl);
        return RedirectToAction(nameof(Index));
    }

    // A-8: kẹp số lượng theo tồn kho phía server giống Add(), không chỉ dựa vào
    // thuộc tính max ở input phía client.
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(int id, int qty)
    {
        var cid = User.CustomerId();
        if (cid.HasValue)
        {
            await MergeSessionIfAnyAsync(cid.Value);
            var result = await _cart.UpdateAsync(cid.Value, id, qty);
            if (result.Outcome == CartService.CartUpdateOutcome.OutOfStock)
                TempData["Warning"] = "Sản phẩm đã hết hàng hoặc ngừng bán nên đã được bỏ khỏi giỏ.";
            else if (result.Outcome == CartService.CartUpdateOutcome.Clamped)
                TempData["Warning"] = $"\"{result.ProductName}\" chỉ còn {result.Stock} chiếc nên số lượng đã giảm xuống {result.Stock}.";
            return RedirectToAction(nameof(Index));
        }

        var cart = HttpContext.Session.GetCart();
        var line = cart.FirstOrDefault(c => c.ProductId == id);
        if (line != null)
        {
            if (qty <= 0)
            {
                cart.Remove(line);
            }
            else
            {
                var product = await _db.Products.FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
                if (product == null || product.Stock <= 0)
                {
                    cart.Remove(line);
                    TempData["Warning"] = "Sản phẩm đã hết hàng hoặc ngừng bán nên đã được bỏ khỏi giỏ.";
                }
                else if (qty > product.Stock)
                {
                    line.Quantity = product.Stock;
                    TempData["Warning"] = $"\"{product.Name}\" chỉ còn {product.Stock} chiếc nên số lượng đã giảm xuống {product.Stock}.";
                }
                else line.Quantity = qty;
            }
            HttpContext.Session.SaveCart(cart);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Remove(int id)
    {
        var cid = User.CustomerId();
        if (cid.HasValue)
        {
            await MergeSessionIfAnyAsync(cid.Value);
            await _cart.RemoveAsync(cid.Value, id);
        }
        else
        {
            var cart = HttpContext.Session.GetCart();
            cart.RemoveAll(c => c.ProductId == id);
            HttpContext.Session.SaveCart(cart);
        }
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Checkout(string voucherCode = null)
    {
        // A-7: claim CustomerId có thể thiếu (tài khoản tạo trực tiếp trong DB) —
        // không dùng .Value mà kiểm tra null và đưa về đăng nhập lại.
        var cid = User.CustomerId();
        if (!cid.HasValue) return RedirectToAction("Login", "Account");

        var items = await BuildItems();
        if (!items.Any()) return RedirectToAction(nameof(Index));

        var customer = await _db.Customers.FindAsync(cid.Value);
        // Vòng 2: chỉ liệt kê COD + chuyển khoản (không làm thanh toán online).
        // Mặc định chọn hình thức đầu tiên (B-9).
        var methods = await GetAllowedPaymentMethodsAsync();
        var vm = new CheckoutVm
        {
            ReceiverName = customer?.FullName,
            Phone = customer?.Phone,
            ShippingAddress = customer?.Address,
            Items = items,
            PaymentMethods = methods,
            PaymentMethodId = methods.FirstOrDefault()?.Id ?? 0
        };

        // Vòng 2: voucher khách nhập qua ?voucherCode=... (nút "Áp dụng" ở form checkout);
        // validate lại phía server, sai thì báo lỗi nhưng vẫn hiện form để nhập lại.
        Voucher voucher = null;
        if (!string.IsNullOrWhiteSpace(voucherCode))
        {
            vm.VoucherCode = voucherCode.Trim().ToUpperInvariant();
            var (v, error) = await _vouchers.ValidateAsync(vm.VoucherCode, vm.Total, DateTime.Now);
            if (error != null) ViewBag.VoucherError = error;
            else
            {
                voucher = v;
                ViewBag.VoucherOk = $"Đã áp dụng mã {v.Code}.";
            }
        }

        var pricing = await PriceItemsAsync(items, voucher);
        vm.ShippingFee = pricing.ShippingFee;
        vm.DiscountAmount = pricing.DiscountAmount;
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Checkout(CheckoutVm m)
    {
        // A-7: kiểm tra null thay vì User.CustomerId().Value.
        var cid = User.CustomerId();
        if (!cid.HasValue) return RedirectToAction("Login", "Account");

        var items = await BuildItems();
        if (!items.Any()) return RedirectToAction(nameof(Index));

        var subtotal = items.Sum(i => i.LineTotal);

        // Vòng 2: validate voucher phía server (không tin mã client gửi kèm) và
        // kiểm tra hình thức thanh toán — chỉ chấp nhận COD/chuyển khoản (nhóm không
        // làm thanh toán online nên từ chối thẻ/MoMo).
        Voucher voucher = null;
        if (!string.IsNullOrWhiteSpace(m.VoucherCode))
        {
            var code = m.VoucherCode.Trim().ToUpperInvariant();
            var (v, error) = await _vouchers.ValidateAsync(code, subtotal, DateTime.Now);
            if (error != null) ModelState.AddModelError(nameof(m.VoucherCode), error);
            else { voucher = v; m.VoucherCode = code; }
        }

        var method = await _db.PaymentMethods.FindAsync(m.PaymentMethodId);
        if (method == null)
            ModelState.AddModelError(nameof(m.PaymentMethodId), "Vui lòng chọn hình thức thanh toán");
        else if (!IsAllowedPaymentMethod(method))
            ModelState.AddModelError(nameof(m.PaymentMethodId),
                "Hình thức thanh toán này hiện chưa được hỗ trợ. Thanh toán online không thực hiện theo yêu cầu của nhóm — chỉ dùng COD hoặc chuyển khoản.");

        if (!ModelState.IsValid)
        {
            m.Items = items;
            m.PaymentMethods = await GetAllowedPaymentMethodsAsync();
            var pricing = await PriceItemsAsync(items, voucher);
            m.ShippingFee = pricing.ShippingFee;
            m.DiscountAmount = pricing.DiscountAmount;
            return View(m);
        }

        // Vòng 2: tính lại phí ship/giảm giá/tổng cuối phía server (không tin số client gửi).
        var finalPricing = await PriceItemsAsync(items, voucher);

        var order = new Order
        {
            CustomerId = cid.Value,
            PaymentMethodId = m.PaymentMethodId,
            ReceiverName = m.ReceiverName,
            Phone = m.Phone,
            ShippingAddress = m.ShippingAddress,
            Note = m.Note,
            Status = OrderStatus.Pending,
            ShippingFee = finalPricing.ShippingFee,
            DiscountAmount = finalPricing.DiscountAmount,
            VoucherCode = voucher?.Code
        };

        // A-1: toàn bộ checkout chạy trong MỘT transaction tường minh. Tồn kho được
        // trừ bằng lệnh UPDATE có điều kiện ở tầng DB (Stock >= Quantity); số dòng bị
        // ảnh hưởng = 0 nghĩa là có người khác vừa mua hết hàng giữa chừng → rollback
        // toàn bộ và báo lỗi rõ ràng, không tạo đơn thiếu hàng. Lệnh UPDATE có điều
        // kiện là nguyên tử ở DB nên hai checkout đồng thời không thể cùng trừ 1 chiếc
        // cuối cùng. (Không dùng RowVersion: ExecuteUpdateAsync + kiểm tra affected rows
        // đã đủ và không cần thêm cột vào bảng Products.)
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var it in items)
            {
                var qty = it.Quantity;
                var affected = await _db.Products
                    .Where(p => p.Id == it.Product.Id && p.Stock >= qty)
                    .ExecuteUpdateAsync(s => s.SetProperty(p => p.Stock, p => p.Stock - qty));
                if (affected == 0)
                {
                    await tx.RollbackAsync();
                    _logger.LogWarning("Checkout bị hủy: sản phẩm {ProductId} ({Name}) vừa hết hàng.", it.Product.Id, it.Product.Name);
                    TempData["Error"] = $"\"{it.Product.Name}\" vừa hết hàng (có khách khác vừa mua hết). Vui lòng kiểm tra lại giỏ hàng.";
                    return RedirectToAction(nameof(Index));
                }
                order.Details.Add(new OrderDetail { ProductId = it.Product.Id, Quantity = qty, UnitPrice = it.Product.Price });
            }
            // Vòng 2: Total là tổng cuối khách phải trả = tạm tính + phí ship - giảm giá.
            order.Total = finalPricing.GrandTotal;

            // Vòng 2: tăng UsedCount CHỈ khi đơn được tạo thành công, trong cùng
            // transaction tạo đơn (voucher đã validate ở trên, cùng DbContext nên
            // entity vẫn được theo dõi — chỉ tăng số đếm, không lưu lại entity khác).
            if (voucher != null) voucher.UsedCount++;

            _db.Orders.Add(order);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi đặt hàng cho khách {CustomerId}.", cid.Value);
            TempData["Error"] = "Đã có lỗi khi đặt hàng. Vui lòng thử lại sau.";
            return RedirectToAction(nameof(Index));
        }

        // Vòng 2: giỏ bền lưu trong DB nên xóa giỏ qua CartService (thay cho
        // HttpContext.Session.ClearCart() trước đây). Đây là dòng duy nhất
        // được đổi trong Checkout POST về phía giỏ — logic transaction/trừ kho/
        // tạo đơn ở trên giữ nguyên.
        await _cart.ClearAsync(cid.Value);

        return RedirectToAction(nameof(Success), new { id = order.Id });
    }

    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Success(int id)
    {
        var order = await _db.Orders.Include(o => o.PaymentMethod)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == User.CustomerId());
        if (order == null) return NotFound();

        // Chuyển khoản: hiện hướng dẫn liên hệ hotline (không có số tài khoản trong code).
        if (order.PaymentMethod?.Name == "Chuyển khoản ngân hàng")
            ViewBag.BankTransferLines = BankTransferInstructionLines(order);
        return View(order);
    }
}
