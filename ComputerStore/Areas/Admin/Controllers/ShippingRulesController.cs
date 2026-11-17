using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

/// <summary>CRUD quy tắc phí vận chuyển phía admin (vòng 2).</summary>
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ShippingRulesController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public ShippingRulesController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1)
    {
        var total = await _db.ShippingRules.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        var rules = await _db.ShippingRules.OrderBy(r => r.SortOrder).ThenBy(r => r.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(rules);
    }

    [HttpGet]
    public IActionResult Create() =>
        View("Form", new ShippingRule { IsActive = true, SortOrder = 1 });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Name,FreeFromAmount,Fee,IsActive,SortOrder")] ShippingRule model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        _db.ShippingRules.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm quy tắc phí ship \"{model.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var rule = await _db.ShippingRules.FindAsync(id);
        return rule == null ? NotFound() : View("Form", rule);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,Name,FreeFromAmount,Fee,IsActive,SortOrder")] ShippingRule model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Form", model);
        var rule = await _db.ShippingRules.FindAsync(id);
        if (rule == null) return NotFound();
        rule.Name = model.Name;
        rule.FreeFromAmount = model.FreeFromAmount;
        rule.Fee = model.Fee;
        rule.IsActive = model.IsActive;
        rule.SortOrder = model.SortOrder;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật quy tắc phí ship.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var rule = await _db.ShippingRules.FindAsync(id);
        if (rule == null) return NotFound();
        // Phí ship đã tính được sao chép vào Order (ShippingFee) nên xóa quy tắc
        // không ảnh hưởng đơn cũ; chỉ lưu ý khi xóa hết quy tắc thì phí ship = 0.
        _db.ShippingRules.Remove(rule);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa quy tắc \"{rule.Name}\".";
        return RedirectToAction(nameof(Index));
    }
}
