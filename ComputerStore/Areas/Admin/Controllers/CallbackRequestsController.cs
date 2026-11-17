using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

/// <summary>Danh sách yêu cầu gọi lại + đánh dấu đã xử lý (A-4).</summary>
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class CallbackRequestsController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public CallbackRequestsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(bool? handled, int page = 1)
    {
        var query = _db.CallbackRequests.AsQueryable();
        if (handled.HasValue) query = query.Where(r => r.IsHandled == handled.Value);
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Handled = handled; ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        var items = await query.OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(items);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkHandled(int id, bool handled)
    {
        var req = await _db.CallbackRequests.FindAsync(id);
        if (req == null) return NotFound();
        req.IsHandled = handled;
        await _db.SaveChangesAsync();
        TempData["Success"] = handled ? "Đã đánh dấu đã xử lý." : "Đã đánh dấu chưa xử lý.";
        return RedirectToAction(nameof(Index));
    }
}
