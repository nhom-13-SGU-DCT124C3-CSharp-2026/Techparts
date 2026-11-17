using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class ProductsController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    private readonly IWebHostEnvironment _env;
    private readonly ILogger<ProductsController> _logger;

    public ProductsController(AppDbContext db, IWebHostEnvironment env, ILogger<ProductsController> logger)
    {
        _db = db;
        _env = env;
        _logger = logger;
    }

    private async Task LoadLookups(Product p = null)
    {
        ViewBag.Categories = new SelectList(await _db.Categories.OrderBy(x => x.Name).ToListAsync(), "Id", "Name", p?.CategoryId);
        ViewBag.Groups = new SelectList(await _db.ProductGroups.OrderBy(x => x.Name).ToListAsync(), "Id", "Name", p?.GroupId);
        ViewBag.Brands = new SelectList(await _db.Brands.OrderBy(x => x.Name).ToListAsync(), "Id", "Name", p?.BrandId);
    }

    // B-10: kiểm tra magic bytes (nội dung thật của file), không chỉ đuôi file.
    // JPEG: FF D8 FF · PNG: 89 50 4E 47 0D 0A 1A 0A · GIF: "GIF8" · WebP: "RIFF....WEBP".
    private static bool HasValidImageSignature(byte[] header, string ext)
    {
        if (header == null || header.Length < 12) return false;
        return ext switch
        {
            ".jpg" or ".jpeg" => header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
            ".png" => header[0] == 0x89 && header[1] == 0x50 && header[2] == 0x4E && header[3] == 0x47,
            ".gif" => header[0] == 0x47 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x38,
            ".webp" => header[0] == 0x52 && header[1] == 0x49 && header[2] == 0x46 && header[3] == 0x46
                        && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50,
            _ => false
        };
    }

    private async Task<string> SaveImage(IFormFile file)
    {
        if (file == null || file.Length == 0) return null;
        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!new[] { ".jpg", ".jpeg", ".png", ".webp", ".gif" }.Contains(ext)) return null;
        if (file.Length > 5 * 1024 * 1024) return null;

        try
        {
            // Đọc 12 byte đầu để kiểm tra magic bytes, rồi tua lại đầu stream để lưu.
            await using var stream = file.OpenReadStream();
            var header = new byte[12];
            var read = await stream.ReadAsync(header, 0, header.Length);
            if (read < 12 || !HasValidImageSignature(header, ext))
            {
                _logger.LogWarning("Từ chối upload ảnh: nội dung file không khớp đuôi {Ext} (file {FileName}).", ext, file.FileName);
                return null;
            }
            stream.Position = 0;

            var dir = Path.Combine(_env.WebRootPath, "uploads");
            Directory.CreateDirectory(dir);
            var name = Guid.NewGuid().ToString("N") + ext;
            await using var fs = new FileStream(Path.Combine(dir, name), FileMode.Create);
            await stream.CopyToAsync(fs);
            return "/uploads/" + name;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu ảnh upload {FileName}.", file.FileName);
            return null;
        }
    }

    private void DeleteImage(string url)
    {
        if (string.IsNullOrEmpty(url) || !url.StartsWith("/uploads/")) return;
        var path = Path.Combine(_env.WebRootPath, "uploads", Path.GetFileName(url));
        if (System.IO.File.Exists(path)) System.IO.File.Delete(path);
    }

    public async Task<IActionResult> Index(string q, int? categoryId, int? brandId, int page = 1)
    {
        var query = _db.Products.Include(p => p.Category).Include(p => p.Brand).Include(p => p.Group).AsQueryable();
        if (!string.IsNullOrWhiteSpace(q)) query = query.Where(p => p.Name.Contains(q) || p.Sku.Contains(q));
        if (categoryId.HasValue) query = query.Where(p => p.CategoryId == categoryId);
        if (brandId.HasValue) query = query.Where(p => p.BrandId == brandId);

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        await LoadLookups(new Product { CategoryId = categoryId ?? 0, BrandId = brandId ?? 0 });
        ViewBag.Q = q; ViewBag.CategoryId = categoryId; ViewBag.BrandId = brandId;
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        return View(await query.OrderByDescending(p => p.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync());
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var p = new Product();
        await LoadLookups(p);
        return View("Form", p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Name,Sku,Description,Specifications,Price,OldPrice,WarrantyMonths,IsActive,CategoryId,GroupId,BrandId")] Product model,
        IFormFile imageFile)
    {
        if (await _db.Products.AnyAsync(p => p.Sku == model.Sku))
            ModelState.AddModelError("Sku", "Mã SKU đã tồn tại");
        if (!await _db.Categories.AnyAsync(x => x.Id == model.CategoryId)) ModelState.AddModelError("CategoryId", "Chọn loại linh kiện");
        if (!await _db.ProductGroups.AnyAsync(x => x.Id == model.GroupId)) ModelState.AddModelError("GroupId", "Chọn nhóm linh kiện");
        if (!await _db.Brands.AnyAsync(x => x.Id == model.BrandId)) ModelState.AddModelError("BrandId", "Chọn thương hiệu");

        if (imageFile != null && imageFile.Length > 0)
        {
            model.ImageUrl = await SaveImage(imageFile);
            if (model.ImageUrl == null) ModelState.AddModelError("", "Ảnh không hợp lệ (jpg, png, webp, gif - tối đa 5MB, đúng định dạng ảnh thật)");
        }

        if (!ModelState.IsValid)
        {
            await LoadLookups(model);
            return View("Form", model);
        }

        model.Stock = 0; // Tồn kho chỉ thay đổi qua chức năng Nhập kho / bán hàng
        _db.Products.Add(model);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi khi thêm linh kiện {Sku}.", model.Sku);
            ModelState.AddModelError("", "Không lưu được linh kiện. Vui lòng kiểm tra lại dữ liệu và thử lại.");
            await LoadLookups(model);
            return View("Form", model);
        }
        TempData["Success"] = $"Đã thêm linh kiện \"{model.Name}\". Hãy nhập kho để có hàng bán.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();
        await LoadLookups(p);
        return View("Form", p);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,Name,Sku,Description,Specifications,Price,OldPrice,WarrantyMonths,IsActive,CategoryId,GroupId,BrandId")] Product model,
        IFormFile imageFile)
    {
        if (id != model.Id) return BadRequest();
        var entity = await _db.Products.FindAsync(id);
        if (entity == null) return NotFound();

        if (await _db.Products.AnyAsync(p => p.Sku == model.Sku && p.Id != id))
            ModelState.AddModelError("Sku", "Mã SKU đã tồn tại");

        string newImage = null;
        if (imageFile != null && imageFile.Length > 0)
        {
            newImage = await SaveImage(imageFile);
            if (newImage == null) ModelState.AddModelError("", "Ảnh không hợp lệ (jpg, png, webp, gif - tối đa 5MB, đúng định dạng ảnh thật)");
        }

        if (!ModelState.IsValid)
        {
            model.Stock = entity.Stock;
            model.ImageUrl = entity.ImageUrl;
            await LoadLookups(model);
            return View("Form", model);
        }

        entity.Name = model.Name; entity.Sku = model.Sku;
        entity.Description = model.Description; entity.Specifications = model.Specifications;
        entity.Price = model.Price; entity.OldPrice = model.OldPrice; entity.WarrantyMonths = model.WarrantyMonths; entity.IsActive = model.IsActive;
        entity.CategoryId = model.CategoryId; entity.GroupId = model.GroupId; entity.BrandId = model.BrandId;
        if (newImage != null)
        {
            DeleteImage(entity.ImageUrl);
            entity.ImageUrl = newImage;
        }

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi khi sửa linh kiện #{Id}.", id);
            ModelState.AddModelError("", "Không lưu được linh kiện. Vui lòng kiểm tra lại dữ liệu và thử lại.");
            model.Stock = entity.Stock;
            model.ImageUrl = entity.ImageUrl;
            await LoadLookups(model);
            return View("Form", model);
        }
        TempData["Success"] = "Đã cập nhật linh kiện.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var p = await _db.Products.FindAsync(id);
        if (p == null) return NotFound();

        var used = await _db.OrderDetails.AnyAsync(d => d.ProductId == id)
                || await _db.StockReceiptDetails.AnyAsync(d => d.ProductId == id);
        if (used)
        {
            TempData["Error"] = $"\"{p.Name}\" đã phát sinh đơn hàng/phiếu nhập nên không thể xóa. Hãy bỏ chọn \"Đang bán\" để ẩn khỏi cửa hàng.";
            return RedirectToAction(nameof(Index));
        }

        DeleteImage(p.ImageUrl);
        _db.Products.Remove(p);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa \"{p.Name}\".";
        return RedirectToAction(nameof(Index));
    }
}
