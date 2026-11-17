using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Controllers;

public class ShopController : Controller
{
    private const int PageSize = 12;
    private readonly AppDbContext _db;
    public ShopController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(string q, int? categoryId, int? groupId, int? brandId,
        decimal? minPrice, decimal? maxPrice, string sort, int page = 1, bool discounted = false)
    {
        var query = _db.Products.Include(p => p.Brand).Include(p => p.Category)
                                .Where(p => p.IsActive);

        if (!string.IsNullOrWhiteSpace(q))
            query = query.Where(p => p.Name.Contains(q) || p.Sku.Contains(q));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (groupId.HasValue) query = query.Where(p => p.GroupId == groupId);
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);
        if (minPrice.HasValue) query = query.Where(p => p.Price >= minPrice.Value);
        if (maxPrice.HasValue) query = query.Where(p => p.Price <= maxPrice.Value);
        // B-6: chỉ món đang giảm giá (có giá gốc lớn hơn giá bán).
        if (discounted) query = query.Where(p => p.OldPrice.HasValue && p.OldPrice.Value > p.Price);

        // Sắp xếp theo giá: ép sang double để EF Core dịch được trên cả
        // SQL Server (CAST AS float) và SQLite (decimal lưu dạng TEXT nên
        // ORDER BY trực tiếp trên cột decimal gây lỗi 500 — xem FIX_LOG).
        // Giá là số nguyên đồng, double biểu diễn chính xác nên thứ tự không đổi.
        query = sort switch
        {
            "price_asc" => query.OrderBy(p => (double)p.Price),
            "price_desc" => query.OrderByDescending(p => (double)p.Price),
            "name" => query.OrderBy(p => p.Name),
            _ => query.OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id)
        };

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var vm = new ShopIndexVm
        {
            Products = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(),
            Categories = await _db.Categories.OrderBy(x => x.Name).ToListAsync(),
            Groups = await _db.ProductGroups.OrderBy(x => x.Name).ToListAsync(),
            Brands = await _db.Brands.OrderBy(x => x.Name).ToListAsync(),
            Q = q, CategoryId = categoryId, GroupId = groupId, BrandId = brandId,
            MinPrice = minPrice, MaxPrice = maxPrice, Sort = sort,
            Page = page, TotalPages = totalPages, TotalItems = total, Discounted = discounted
        };
        return View(vm);
    }

    public async Task<IActionResult> Details(int id)
    {
        var product = await _db.Products
            .Include(p => p.Brand).Include(p => p.Category).Include(p => p.Group)
            .FirstOrDefaultAsync(p => p.Id == id && p.IsActive);
        if (product == null) return NotFound();

        ViewBag.Related = await _db.Products.Include(p => p.Brand).Include(p => p.Category)
            .Where(p => p.IsActive && p.CategoryId == product.CategoryId && p.Id != id)
            .Take(4).ToListAsync();
        return View(product);
    }
}
