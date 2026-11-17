using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

/// <summary>CRUD mã giảm giá (voucher) phía admin (vòng 2).</summary>
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class VouchersController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public VouchersController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1)
    {
        var total = await _db.Vouchers.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        var vouchers = await _db.Vouchers.OrderBy(v => v.Code)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(vouchers);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new Voucher
    {
        ValidFrom = DateTime.Now,
        ValidTo = new DateTime(DateTime.Now.Year, 12, 31, 23, 59, 59),
        MaxUses = 100,
        IsActive = true
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(
        [Bind("Code,DiscountType,DiscountValue,MinOrderAmount,MaxDiscountAmount,ValidFrom,ValidTo,MaxUses,IsActive")] Voucher model)
    {
        Normalize(model);
        if (await _db.Vouchers.AnyAsync(v => v.Code == model.Code))
            ModelState.AddModelError("Code", "Mã voucher đã tồn tại.");
        ValidateBusinessRules(model);
        if (!ModelState.IsValid) return View("Form", model);

        _db.Vouchers.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã thêm mã giảm giá \"{model.Code}\".";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        return voucher == null ? NotFound() : View("Form", voucher);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id,
        [Bind("Id,Code,DiscountType,DiscountValue,MinOrderAmount,MaxDiscountAmount,ValidFrom,ValidTo,MaxUses,IsActive")] Voucher model)
    {
        if (id != model.Id) return BadRequest();
        Normalize(model);
        if (await _db.Vouchers.AnyAsync(v => v.Code == model.Code && v.Id != id))
            ModelState.AddModelError("Code", "Mã voucher đã tồn tại.");
        ValidateBusinessRules(model);
        if (!ModelState.IsValid) return View("Form", model);

        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null) return NotFound();
        voucher.Code = model.Code;
        voucher.DiscountType = model.DiscountType;
        voucher.DiscountValue = model.DiscountValue;
        voucher.MinOrderAmount = model.MinOrderAmount;
        voucher.MaxDiscountAmount = model.MaxDiscountAmount;
        voucher.ValidFrom = model.ValidFrom;
        voucher.ValidTo = model.ValidTo;
        voucher.MaxUses = model.MaxUses;
        voucher.IsActive = model.IsActive;
        // UsedCount không cho sửa tay — chỉ tăng khi đơn được tạo thành công.
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật mã giảm giá.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var voucher = await _db.Vouchers.FindAsync(id);
        if (voucher == null) return NotFound();
        // Mã đã có người dùng thì không xóa (giữ lại đối chiếu với đơn cũ);
        // muốn dừng thì bỏ tick "Đang áp dụng".
        if (voucher.UsedCount > 0)
        {
            TempData["Error"] = $"Không thể xóa mã \"{voucher.Code}\" vì đã được dùng {voucher.UsedCount} lần. Hãy bỏ tick \"Đang áp dụng\" để dừng mã này.";
            return RedirectToAction(nameof(Index));
        }
        _db.Vouchers.Remove(voucher);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Đã xóa mã giảm giá \"{voucher.Code}\".";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Chuẩn hóa mã (chữ hoa, không khoảng trắng); loại Fixed không dùng giảm tối đa.</summary>
    private static void Normalize(Voucher model)
    {
        model.Code = (model.Code ?? "").Trim().ToUpperInvariant();
        if (model.DiscountType == DiscountType.Fixed) model.MaxDiscountAmount = null;
    }

    /// <summary>Kiểm tra quy tắc nghiệp vụ mà DataAnnotations không diễn đạt được.</summary>
    private void ValidateBusinessRules(Voucher model)
    {
        if (model.DiscountType == DiscountType.Percent
            && (model.DiscountValue <= 0 || model.DiscountValue > 100))
            ModelState.AddModelError("DiscountValue", "Giảm theo % phải từ 1 đến 100.");
        if (model.DiscountType == DiscountType.Fixed && model.DiscountValue <= 0)
            ModelState.AddModelError("DiscountValue", "Số tiền giảm phải lớn hơn 0.");
        if (model.ValidTo < model.ValidFrom)
            ModelState.AddModelError("ValidTo", "Hạn cuối phải sau ngày bắt đầu hiệu lực.");
        if (model.MaxUses < 1)
            ModelState.AddModelError("MaxUses", "Số lượt tối đa phải từ 1 trở lên.");
    }
}
