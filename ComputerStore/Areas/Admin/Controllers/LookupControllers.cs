using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

/// <summary>
/// Controller CRUD dùng chung cho các danh mục đơn giản (Tên + Mô tả):
/// Loại linh kiện, Nhóm linh kiện, Thương hiệu, Hình thức thanh toán.
/// </summary>
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public abstract class LookupController<T> : Controller where T : LookupEntity, new()
{
    protected readonly AppDbContext _db;
    protected LookupController(AppDbContext db) => _db = db;

    protected abstract string Title { get; }                 // Tiêu đề hiển thị
    protected abstract Task<bool> IsInUse(int id);           // Đã được sử dụng -> không cho xóa
    protected DbSet<T> Set => _db.Set<T>();

    private const string IndexView = "~/Areas/Admin/Views/Shared/LookupIndex.cshtml";
    private const string FormView = "~/Areas/Admin/Views/Shared/LookupForm.cshtml";

    public async Task<IActionResult> Index(string q)
    {
        ViewData["Heading"] = Title;
        ViewData["Q"] = q;
        var query = Set.AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(x => x.Name.Contains(q));
        return View(IndexView, await query.OrderBy(x => x.Name).ToListAsync());
    }

    [HttpGet]
    public IActionResult Create()
    {
        ViewData["Heading"] = Title;
        return View(FormView, new T());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Description")] T model)
    {
        ViewData["Heading"] = Title;
        if (await Set.AnyAsync(x => x.Name == model.Name))
            ModelState.AddModelError("Name", "Tên đã tồn tại");
        if (!ModelState.IsValid) return View(FormView, model);

        Set.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm {Title.ToLower()} \"{model.Name}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await Set.FindAsync(id);
        if (entity == null) return NotFound();
        ViewData["Heading"] = Title;
        return View(FormView, entity);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Name,Description")] T model)
    {
        ViewData["Heading"] = Title;
        if (id != model.Id) return BadRequest();
        if (await Set.AnyAsync(x => x.Name == model.Name && x.Id != id))
            ModelState.AddModelError("Name", "Tên đã tồn tại");
        if (!ModelState.IsValid) return View(FormView, model);

        var entity = await Set.FindAsync(id);
        if (entity == null) return NotFound();
        entity.Name = model.Name;
        entity.Description = model.Description;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await Set.FindAsync(id);
        if (entity == null) return NotFound();

        if (await IsInUse(id))
        {
            TempData["Error"] = $"Không thể xóa \"{entity.Name}\" vì đang được sử dụng.";
            return RedirectToAction(nameof(Index));
        }

        Set.Remove(entity);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa \"{entity.Name}\".";
        return RedirectToAction(nameof(Index));
    }
}

// B-1: gắn [Authorize] tường minh lên từng controller con thay vì chỉ dựa vào
// việc attribute trên class cha được kế thừa.
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class CategoriesController : LookupController<Category>
{
    public CategoriesController(AppDbContext db) : base(db) { }
    protected override string Title => "Loại linh kiện";
    protected override Task<bool> IsInUse(int id) => _db.Products.AnyAsync(p => p.CategoryId == id);
}

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class GroupsController : LookupController<ProductGroup>
{
    public GroupsController(AppDbContext db) : base(db) { }
    protected override string Title => "Nhóm linh kiện";
    protected override Task<bool> IsInUse(int id) => _db.Products.AnyAsync(p => p.GroupId == id);
}

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class BrandsController : LookupController<Brand>
{
    public BrandsController(AppDbContext db) : base(db) { }
    protected override string Title => "Thương hiệu";
    protected override Task<bool> IsInUse(int id) => _db.Products.AnyAsync(p => p.BrandId == id);
}

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class PaymentMethodsController : LookupController<PaymentMethod>
{
    public PaymentMethodsController(AppDbContext db) : base(db) { }
    protected override string Title => "Hình thức thanh toán";
    protected override Task<bool> IsInUse(int id) => _db.Orders.AnyAsync(o => o.PaymentMethodId == id);
}
