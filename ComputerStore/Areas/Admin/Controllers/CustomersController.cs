using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

// Theo yêu cầu: chỉ Thêm / Sửa thông tin khách hàng (không xóa để giữ lịch sử đơn hàng)
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class CustomersController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public CustomersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string q, int page = 1)
    {
        var query = _db.Customers.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(c => c.FullName.Contains(q) || c.Phone.Contains(q) || c.Email.Contains(q));
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Q = q; ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(await query.OrderByDescending(c => c.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var c = await _db.Customers
            .Include(x => x.Orders).ThenInclude(o => o.PaymentMethod)
            .FirstOrDefaultAsync(x => x.Id == id);
        return c == null ? NotFound() : View(c);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new Customer());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("FullName,Email,Phone,Address")] Customer model)
    {
        // B-3: không cho trùng Email/SĐT với khách đã có.
        if (!string.IsNullOrWhiteSpace(model.Email) && await _db.Customers.AnyAsync(c => c.Email == model.Email))
            ModelState.AddModelError(nameof(model.Email), "Email này đã được dùng bởi khách hàng khác");
        if (await _db.Customers.AnyAsync(c => c.Phone == model.Phone))
            ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã được dùng bởi khách hàng khác");
        if (!ModelState.IsValid) return View("Form", model);
        _db.Customers.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã thêm khách hàng.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var c = await _db.Customers.FindAsync(id);
        return c == null ? NotFound() : View("Form", c);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,FullName,Email,Phone,Address")] Customer model)
    {
        if (id != model.Id) return BadRequest();
        if (!string.IsNullOrWhiteSpace(model.Email) && await _db.Customers.AnyAsync(c => c.Email == model.Email && c.Id != id))
            ModelState.AddModelError(nameof(model.Email), "Email này đã được dùng bởi khách hàng khác");
        if (await _db.Customers.AnyAsync(c => c.Phone == model.Phone && c.Id != id))
            ModelState.AddModelError(nameof(model.Phone), "Số điện thoại này đã được dùng bởi khách hàng khác");
        if (!ModelState.IsValid) return View("Form", model);

        var c = await _db.Customers.FindAsync(id);
        if (c == null) return NotFound();
        c.FullName = model.FullName; c.Email = model.Email; c.Phone = model.Phone; c.Address = model.Address;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật khách hàng.";
        return RedirectToAction(nameof(Index));
    }
}
