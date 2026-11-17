using ComputerStore.Data;
using ComputerStore.Helpers;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class StockReceiptsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<StockReceiptsController> _logger;
    public StockReceiptsController(AppDbContext db, ILogger<StockReceiptsController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index() =>
        View(await _db.StockReceipts.Include(r => r.Details).OrderByDescending(r => r.ReceiptDate).ToListAsync());

    public async Task<IActionResult> Details(int id)
    {
        var r = await _db.StockReceipts.Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        return r == null ? NotFound() : View(r);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        ViewBag.Products = await _db.Products.OrderBy(p => p.Name).ToListAsync();
        return View(new StockReceiptCreateVm());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(StockReceiptCreateVm m)
    {
        var items = (m.Items ?? new()).Where(i => i.ProductId > 0).ToList();

        if (!items.Any()) ModelState.AddModelError("", "Phiếu nhập phải có ít nhất 1 linh kiện");
        if (items.Any(i => i.Quantity <= 0)) ModelState.AddModelError("", "Số lượng nhập phải lớn hơn 0");
        if (items.Any(i => i.UnitCost < 0)) ModelState.AddModelError("", "Giá nhập không hợp lệ");
        // B-4: giới hạn trên cho số lượng/giá nhập (tránh nhập nhầm số quá lớn).
        if (items.Any(i => i.Quantity > 10000)) ModelState.AddModelError("", "Số lượng nhập mỗi dòng tối đa 10.000");
        if (items.Any(i => i.UnitCost > 1000000000m)) ModelState.AddModelError("", "Giá nhập mỗi dòng tối đa 1.000.000.000 ₫");

        // B-5: TỪ CHỐI phiếu có cùng 1 sản phẩm ở nhiều dòng (thay vì lặng lẽ gộp và
        // lấy giá của dòng cuối). Người nhập phải gộp lại thành 1 dòng với 1 giá nhập.
        var dupIds = items.GroupBy(i => i.ProductId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupIds.Any())
        {
            var names = await _db.Products.Where(p => dupIds.Contains(p.Id)).Select(p => p.Name).ToListAsync();
            ModelState.AddModelError("", "Phiếu nhập có sản phẩm bị trùng dòng: " + string.Join(", ", names)
                + ". Vui lòng gộp thành một dòng với một giá nhập duy nhất.");
        }

        var ids = items.Select(i => i.ProductId).Distinct().ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
        if (products.Count != ids.Count) ModelState.AddModelError("", "Có linh kiện không tồn tại");

        if (!ModelState.IsValid)
        {
            ViewBag.Products = await _db.Products.OrderBy(p => p.Name).ToListAsync();
            return View(m);
        }

        var receipt = new StockReceipt { Supplier = m.Supplier, Note = m.Note };
        foreach (var it in items)
        {
            receipt.Details.Add(new StockReceiptDetail
            {
                ProductId = it.ProductId, Quantity = it.Quantity, UnitCost = it.UnitCost
            });
            products.First(p => p.Id == it.ProductId).Stock += it.Quantity;   // Tăng tồn kho
        }
        receipt.Total = receipt.Details.Sum(d => d.Quantity * d.UnitCost);

        _db.StockReceipts.Add(receipt);
        try
        {
            await _db.SaveChangesAsync();   // lưu phiếu + cập nhật tồn kho trong cùng 1 transaction
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Lỗi khi lưu phiếu nhập kho.");
            ModelState.AddModelError("", "Không lưu được phiếu nhập. Vui lòng thử lại.");
            ViewBag.Products = await _db.Products.OrderBy(p => p.Name).ToListAsync();
            return View(m);
        }

        TempData["Success"] = $"Đã nhập kho phiếu #{receipt.Id}.";
        return RedirectToAction(nameof(Details), new { id = receipt.Id });
    }

    // ================= B-13: Sửa / Xóa phiếu nhập kho =================
    // Quyết định thiết kế (ghi rõ theo yêu cầu):
    // - Được sửa: nhà cung cấp, ghi chú, số lượng và đơn giá từng dòng.
    // - KHÔNG được đổi sản phẩm của dòng đã có: tránh nhầm lẫn khi đảo ngược
    //   tồn kho (đổi sản phẩm = xóa dòng cũ + thêm dòng mới, dễ nhập nhầm).
    //   Muốn đổi sản phẩm thì xóa dòng cũ rồi thêm dòng mới.
    // - Được thêm dòng mới (chọn sản phẩm chưa có trong phiếu) và xóa dòng.
    // - Mọi thay đổi tồn kho đều tính trước bằng StockReceiptStock.Check:
    //   nếu bất kỳ sản phẩm nào bị trừ xuống âm (hàng đã bán sau khi nhập)
    //   thì KHÔNG lưu gì cả, báo lỗi nêu rõ tên + tồn kho hiện tại + số
    //   lượng cần đảo ngược, ghi log Warning.

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var r = await _db.StockReceipts.Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();

        var vm = new StockReceiptEditVm
        {
            Id = r.Id,
            ReceiptDate = r.ReceiptDate,
            Supplier = r.Supplier,
            Note = r.Note,
            Lines = r.Details.Select(d => new StockReceiptEditLineVm
            {
                Id = d.Id, ProductId = d.ProductId,
                ProductName = d.Product.Name, Sku = d.Product.Sku,
                Quantity = d.Quantity, UnitCost = d.UnitCost
            }).ToList()
        };
        await LoadAvailableProducts(vm);
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, StockReceiptEditVm m)
    {
        var r = await _db.StockReceipts.Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();
        if (id != m.Id) return BadRequest();
        m.ReceiptDate = r.ReceiptDate;   // ngày nhập không sửa được, không có trong form -> nạp lại để render lại view sau lỗi không bị 01/01/0001

        var lines = (m.Lines ?? new()).Where(l => l.ProductId > 0).ToList();

        // Validate giống Create (giữ nguyên các giới hạn B-4/B-5).
        if (!lines.Any()) ModelState.AddModelError("", "Phiếu nhập phải có ít nhất 1 linh kiện");
        if (lines.Any(l => l.Quantity <= 0)) ModelState.AddModelError("", "Số lượng nhập phải lớn hơn 0");
        if (lines.Any(l => l.UnitCost < 0)) ModelState.AddModelError("", "Giá nhập không hợp lệ");
        if (lines.Any(l => l.Quantity > 10000)) ModelState.AddModelError("", "Số lượng nhập mỗi dòng tối đa 10.000");
        if (lines.Any(l => l.UnitCost > 1000000000m)) ModelState.AddModelError("", "Giá nhập mỗi dòng tối đa 1.000.000.000 ₫");

        var dupIds = lines.GroupBy(l => l.ProductId).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (dupIds.Any())
        {
            var names = await _db.Products.Where(p => dupIds.Contains(p.Id)).Select(p => p.Name).ToListAsync();
            ModelState.AddModelError("", "Phiếu nhập có sản phẩm bị trùng dòng: " + string.Join(", ", names)
                + ". Vui lòng gộp thành một dòng với một giá nhập duy nhất.");
        }

        // Dòng đã có phải thuộc đúng phiếu này và không được đổi sản phẩm.
        var oldById = r.Details.ToDictionary(d => d.Id);
        foreach (var l in lines.Where(l => l.Id != 0))
        {
            if (!oldById.TryGetValue(l.Id, out var old) || old.ProductId != l.ProductId)
            {
                ModelState.AddModelError("", "Có dòng phiếu không hợp lệ (dòng không thuộc phiếu này hoặc đã đổi sản phẩm của dòng đã có).");
                break;
            }
        }

        // Nạp sản phẩm cho cả dòng mới gửi lên VÀ dòng cũ đã bị xóa khỏi form
        // (dòng bị xóa vẫn cần Product để tính đảo ngược tồn kho).
        var postedIds = lines.Select(l => l.ProductId).Distinct().ToList();
        var ids = postedIds.Union(r.Details.Select(d => d.ProductId)).ToList();
        var products = await _db.Products.Where(p => ids.Contains(p.Id)).ToListAsync();
        if (products.Count(p => postedIds.Contains(p.Id)) != postedIds.Count) ModelState.AddModelError("", "Có linh kiện không tồn tại");

        if (!ModelState.IsValid)
        {
            await ReloadLineNames(m, products);
            await LoadAvailableProducts(m);
            return View(m);
        }

        // Tính thay đổi tồn kho ròng theo từng sản phẩm (mới - cũ) rồi kiểm
        // tra TRƯỚC khi đụng vào DB: đảo ngược số lượng cũ rồi áp số mới.
        var deltas = StockReceiptStock.NetDelta(
            r.Details.Select(d => (d.ProductId, d.Quantity)),
            lines.Select(l => (l.ProductId, l.Quantity)));
        var blocked = StockReceiptStock.Check(deltas.Select(kv =>
        {
            var p = products.First(x => x.Id == kv.Key);
            return new StockReceiptStock.StockChange(kv.Key, p.Name, p.Stock, kv.Value);
        }));
        if (blocked.Any())
        {
            var msg = "Không thể sửa phiếu nhập vì sẽ làm tồn kho bị âm (hàng đã bán sau khi nhập): "
                + string.Join("; ", blocked.Select(b =>
                    $"\"{b.ProductName}\" tồn kho hiện tại {b.CurrentStock}, cần đảo ngược {b.QuantityToReverse} chiếc (còn {b.ResultingStock})"));
            _logger.LogWarning("Chặn sửa phiếu nhập #{ReceiptId}: {Message}", r.Id, msg);
            ModelState.AddModelError("", msg);
            await ReloadLineNames(m, products);
            await LoadAvailableProducts(m);
            return View(m);
        }

        // Áp dụng trong 1 transaction: đảo ngược cũ + áp mới + tính lại Total.
        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            r.Supplier = m.Supplier;
            r.Note = m.Note;

            var keptIds = lines.Where(l => l.Id != 0).Select(l => l.Id).ToHashSet();
            var removed = r.Details.Where(d => !keptIds.Contains(d.Id)).ToList();
            _db.StockReceiptDetails.RemoveRange(removed);

            foreach (var l in lines)
            {
                if (l.Id != 0)
                {
                    var d = oldById[l.Id];
                    d.Quantity = l.Quantity;
                    d.UnitCost = l.UnitCost;
                }
                else
                {
                    r.Details.Add(new StockReceiptDetail
                    {
                        ProductId = l.ProductId, Quantity = l.Quantity, UnitCost = l.UnitCost
                    });
                }
            }
            foreach (var (pid, delta) in deltas)
                products.First(p => p.Id == pid).Stock += delta;

            // Tính lại Total từ chính các dòng đã gửi (không dựa vào fixup
            // của navigation collection đối với dòng vừa bị xóa).
            r.Total = lines.Sum(l => l.Quantity * l.UnitCost);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi sửa phiếu nhập #{ReceiptId}.", r.Id);
            ModelState.AddModelError("", "Không lưu được phiếu nhập. Vui lòng thử lại.");
            await ReloadLineNames(m, products);
            await LoadAvailableProducts(m);
            return View(m);
        }

        _logger.LogInformation("Đã sửa phiếu nhập #{ReceiptId}.", r.Id);
        TempData["Success"] = $"Đã cập nhật phiếu nhập PN#{r.Id}.";
        return RedirectToAction(nameof(Details), new { id = r.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Delete(int id)
    {
        var r = await _db.StockReceipts.Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        return r == null ? NotFound() : View(r);
    }

    [HttpPost, ActionName("Delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteConfirmed(int id)
    {
        var r = await _db.StockReceipts.Include(x => x.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (r == null) return NotFound();

        // Kiểm tra TRƯỚC khi đụng vào DB: đảo ngược toàn bộ số lượng đã nhập.
        var blocked = StockReceiptStock.CheckReversal(r.Details.Select(d =>
            (d.ProductId, d.Product.Name, d.Product.Stock, d.Quantity)));
        if (blocked.Any())
        {
            var msg = "Không thể xóa phiếu nhập vì sẽ làm tồn kho bị âm (hàng đã bán sau khi nhập): "
                + string.Join("; ", blocked.Select(b =>
                    $"\"{b.ProductName}\" tồn kho hiện tại {b.CurrentStock}, cần đảo ngược {b.QuantityToReverse} chiếc (còn {b.ResultingStock})"));
            _logger.LogWarning("Chặn xóa phiếu nhập #{ReceiptId}: {Message}", r.Id, msg);
            TempData["Error"] = msg;
            return RedirectToAction(nameof(Details), new { id = r.Id });
        }

        await using var tx = await _db.Database.BeginTransactionAsync();
        try
        {
            foreach (var d in r.Details)
                d.Product.Stock -= d.Quantity;
            _db.StockReceiptDetails.RemoveRange(r.Details);
            _db.StockReceipts.Remove(r);
            await _db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException ex)
        {
            await tx.RollbackAsync();
            _logger.LogError(ex, "Lỗi khi xóa phiếu nhập #{ReceiptId}.", r.Id);
            TempData["Error"] = "Không xóa được phiếu nhập. Vui lòng thử lại.";
            return RedirectToAction(nameof(Details), new { id = r.Id });
        }

        _logger.LogInformation("Đã xóa phiếu nhập #{ReceiptId} (đảo ngược tồn kho).", r.Id);
        TempData["Success"] = $"Đã xóa phiếu nhập PN#{r.Id} và đảo ngược tồn kho.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Sản phẩm chưa có trong phiếu — dùng cho dropdown "thêm dòng" ở form sửa.</summary>
    private async Task LoadAvailableProducts(StockReceiptEditVm m)
    {
        var used = (m.Lines ?? new()).Select(l => l.ProductId).ToHashSet();
        ViewBag.AvailableProducts = await _db.Products
            .Where(p => !used.Contains(p.Id)).OrderBy(p => p.Name).ToListAsync();
    }

    /// <summary>
    /// Nạp lại tên/SKU hiển thị cho các dòng (tên không bind từ form khi sửa
    /// nên phải nạp lại mỗi khi render lại view sau lỗi).
    /// </summary>
    private static Task ReloadLineNames(StockReceiptEditVm m, List<Product> products)
    {
        foreach (var l in m.Lines ?? new())
        {
            var p = products.FirstOrDefault(x => x.Id == l.ProductId);
            if (p != null) { l.ProductName = p.Name; l.Sku = p.Sku; }
        }
        return Task.CompletedTask;
    }
}
