using ComputerStore.Data;
using ComputerStore.Helpers;
using ComputerStore.Models;
using ComputerStore.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Controllers;

/// <summary>
/// Build PC ở mức tối thiểu nhưng chạy được thật (A-5): chọn 1 linh kiện cho mỗi
/// vị trí, hiển thị tổng tiền, kiểm tra tương thích socket CPU/mainboard và chuẩn
/// RAM bằng cách trích token từ trường Specifications. Giới hạn: chỉ kiểm tra được
/// khi thông số ghi rõ socket/chuẩn RAM; không kiểm tra công suất nguồn so với VGA
/// hay kích thước case (thông số mẫu không ghi đủ dữ liệu này) — xem README §5.
/// </summary>
public class BuildPcController : Controller
{
    private readonly AppDbContext _db;
    private readonly CartService _cart;
    public BuildPcController(AppDbContext db, CartService cart)
    {
        _db = db;
        _cart = cart;
    }

    private async Task<List<Product>> ByCategory(string prefix) =>
        await _db.Products.Include(p => p.Brand)
            .Where(p => p.IsActive && p.Stock > 0 && p.Category.Name.StartsWith(prefix))
            // Sắp xếp theo Id (không dùng OrderBy theo Price: SQLite ở môi trường
            // chạy thử không hỗ trợ ORDER BY trên cột decimal, SQL Server thì được).
            .OrderBy(p => p.Id).ToListAsync();

    private async Task<List<Product>> ByCategories(params string[] prefixes) =>
        await _db.Products.Include(p => p.Brand)
            .Where(p => p.IsActive && p.Stock > 0 && prefixes.Any(x => p.Category.Name.StartsWith(x)))
            .OrderBy(p => p.Id).ToListAsync();

    [HttpGet]
    public async Task<IActionResult> Index(int? cpuId, int? mainboardId, int? ramId, int? vgaId,
        int? psuId, int? storageId, int? caseId)
    {
        var vm = new BuildPcVm
        {
            Cpus = await ByCategory("CPU"),
            Mainboards = await ByCategory("Mainboard"),
            Rams = await ByCategory("RAM"),
            Vgas = await ByCategory("VGA"),
            Psus = await ByCategory("PSU"),
            Storages = await ByCategories("SSD", "HDD"),
            Cases = await ByCategory("Case"),
            CpuId = cpuId, MainboardId = mainboardId, RamId = ramId, VgaId = vgaId,
            PsuId = psuId, StorageId = storageId, CaseId = caseId
        };

        var selected = new List<Product>();
        Product Pick(List<Product> list, int? id) => id.HasValue ? list.FirstOrDefault(p => p.Id == id.Value) : null;
        var cpu = Pick(vm.Cpus, cpuId); var mb = Pick(vm.Mainboards, mainboardId);
        var ram = Pick(vm.Rams, ramId); var vga = Pick(vm.Vgas, vgaId);
        var psu = Pick(vm.Psus, psuId); var storage = Pick(vm.Storages, storageId);
        var pcCase = Pick(vm.Cases, caseId);
        foreach (var p in new[] { cpu, mb, ram, vga, psu, storage, pcCase })
            if (p != null) selected.Add(p);

        vm.HasSelection = selected.Any();
        vm.Total = selected.Sum(p => p.Price);

        // --- Kiểm tra tương thích socket CPU vs mainboard ---
        if (cpu != null && mb != null)
        {
            var cpuSocket = Compatibility.ExtractSocket(cpu.Specifications);
            var mbSocket = Compatibility.ExtractSocket(mb.Specifications);
            if (cpuSocket == null || mbSocket == null)
                vm.Warnings.Add("Không xác định được socket của CPU hoặc mainboard từ thông số kỹ thuật, vui lòng kiểm tra lại thông số trước khi mua.");
            else if (cpuSocket != mbSocket)
                vm.Warnings.Add($"Không tương thích: CPU {cpu.Name} dùng socket {cpuSocket} nhưng mainboard {mb.Name} dùng socket {mbSocket}. Hãy chọn mainboard cùng socket {cpuSocket}.");
            else
                vm.Notes.Add($"Tương thích socket: CPU và mainboard đều dùng socket {cpuSocket}.");
        }
        else if (cpu != null || mb != null)
        {
            vm.Notes.Add("Hãy chọn cả CPU và mainboard để kiểm tra socket có khớp nhau không.");
        }

        // --- Kiểm tra chuẩn RAM vs mainboard ---
        if (ram != null && mb != null)
        {
            var ramType = Compatibility.ExtractRamType(ram.Specifications);
            var mbRam = Compatibility.ExtractRamType(mb.Specifications);
            if (ramType == null || mbRam == null)
                vm.Warnings.Add("Không xác định được chuẩn RAM của thanh RAM hoặc mainboard từ thông số kỹ thuật, vui lòng kiểm tra lại thông số trước khi mua.");
            else if (ramType != mbRam)
                vm.Warnings.Add($"Không tương thích: RAM {ram.Name} là {ramType} nhưng mainboard {mb.Name} hỗ trợ {mbRam}. Hãy chọn RAM {mbRam}.");
            else
                vm.Notes.Add($"Tương thích RAM: RAM và mainboard đều dùng {ramType}.");
        }

        if (psu != null)
            vm.Notes.Add($"Nguồn đã chọn: {psu.Name}. Chưa kiểm tra công suất nguồn so với card đồ họa (thông số VGA mẫu không ghi công suất tiêu thụ) — hãy kiểm tra công suất khuyến nghị của VGA trước khi mua.");
        if (vga != null && psu == null)
            vm.Notes.Add("Hãy chọn nguồn (PSU) đủ công suất cho card đồ họa đã chọn.");

        return View(vm);
    }

    // Thêm cả bộ đã chọn vào giỏ hàng (mỗi món 1 chiếc, kẹp theo tồn kho như Cart/Add).
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToCart(int? cpuId, int? mainboardId, int? ramId, int? vgaId,
        int? psuId, int? storageId, int? caseId)
    {
        var ids = new[] { cpuId, mainboardId, ramId, vgaId, psuId, storageId, caseId }
            .Where(i => i.HasValue).Select(i => i.Value).Distinct().ToList();
        if (!ids.Any())
        {
            TempData["Error"] = "Hãy chọn ít nhất một linh kiện trước khi thêm vào giỏ.";
            return RedirectToAction(nameof(Index));
        }

        var products = await _db.Products.Where(p => ids.Contains(p.Id) && p.IsActive && p.Stock > 0).ToListAsync();
        // Vòng 2: khách đã đăng nhập thêm thẳng vào giỏ DB (giỏ bền), vãng lai giữ session như cũ.
        var cid = User.CustomerId();
        if (cid.HasValue)
        {
            var sessionLines = HttpContext.Session.GetCart();
            if (sessionLines.Count > 0)
            {
                await _cart.MergeSessionCartAsync(cid.Value, sessionLines);
                HttpContext.Session.ClearCart();
            }
            foreach (var p in products)
                await _cart.AddAsync(cid.Value, p.Id, 1);
        }
        else
        {
            var cart = HttpContext.Session.GetCart();
            foreach (var p in products)
            {
                var line = cart.FirstOrDefault(c => c.ProductId == p.Id);
                if (line == null) cart.Add(new CartLine { ProductId = p.Id, Quantity = 1 });
                else line.Quantity = Math.Min(line.Quantity + 1, p.Stock);
            }
            HttpContext.Session.SaveCart(cart);
        }
        TempData["Success"] = $"Đã thêm {products.Count} linh kiện của bộ PC vào giỏ hàng.";
        return RedirectToAction("Index", "Cart");
    }
}
