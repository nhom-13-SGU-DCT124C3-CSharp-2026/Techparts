using ComputerStore.Data;
using ComputerStore.Helpers;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class OrdersController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    private readonly ILogger<OrdersController> _logger;
    public OrdersController(AppDbContext db, ILogger<OrdersController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index(OrderStatus? status, string q, int page = 1)
    {
        var query = _db.Orders.Include(o => o.Customer).Include(o => o.PaymentMethod).AsQueryable();
        if (status.HasValue) query = query.Where(o => o.Status == status);
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(o => o.Customer.FullName.Contains(q) || o.Phone.Contains(q));

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Status = status; ViewBag.Q = q; ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        ViewBag.StatusValue = status.HasValue ? (int)status.Value : (int?)null;
        return View(await query.OrderByDescending(o => o.OrderDate)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var o = await _db.Orders.Include(x => x.Customer).Include(x => x.PaymentMethod)
            .Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        return o == null ? NotFound() : View(o);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> UpdateStatus(int id, OrderStatus status)
    {
        var o = await _db.Orders.FindAsync(id);
        if (o == null) return NotFound();

        // A-2: máy trạng thái — chỉ chấp nhận chuyển đổi hợp lệ, từ chối mọi chuyển
        // đổi khác (nhảy bước, đi ngược, gán lại trạng thái cuối...).
        if (!OrderTransitions.IsAllowed(o.Status, status))
        {
            _logger.LogWarning("Từ chối chuyển trạng thái đơn #{OrderId}: {From} -> {To}.", id, o.Status, status);
            TempData["Error"] = $"Không thể chuyển đơn từ \"{o.Status.Text()}\" sang \"{status.Text()}\".";
            return RedirectToAction(nameof(Details), new { id });
        }

        if (status == OrderStatus.Cancelled)
        {
            await OrderHelper.CancelAsync(_db, id);   // hoàn lại tồn kho
            TempData["Success"] = "Đã hủy đơn và hoàn lại tồn kho.";
        }
        else
        {
            o.Status = status;
            if (status == OrderStatus.Completed) o.IsPaid = true;
            await _db.SaveChangesAsync();
            TempData["Success"] = "Đã cập nhật trạng thái đơn hàng.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> TogglePaid(int id)
    {
        var o = await _db.Orders.FindAsync(id);
        if (o == null) return NotFound();
        // A-2: không cho đổi trạng thái thanh toán trên đơn đã hủy.
        if (o.Status == OrderStatus.Cancelled)
        {
            TempData["Error"] = "Đơn đã hủy, không thể thay đổi trạng thái thanh toán.";
            return RedirectToAction(nameof(Details), new { id });
        }
        o.IsPaid = !o.IsPaid;
        await _db.SaveChangesAsync();
        TempData["Success"] = o.IsPaid ? "Đã đánh dấu đã thanh toán." : "Đã đánh dấu chưa thanh toán.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
