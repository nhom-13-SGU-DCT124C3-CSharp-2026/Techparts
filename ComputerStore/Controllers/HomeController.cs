using System.Diagnostics;
using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<HomeController> _logger;
    public HomeController(AppDbContext db, ILogger<HomeController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<IActionResult> Index()
    {
        var categories = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        var groups = await _db.ProductGroups.OrderBy(g => g.Name).ToListAsync();
        var products = await _db.Products
            .Include(p => p.Brand).Include(p => p.Category)
            .Where(p => p.IsActive).ToListAsync();

        int CatId(string prefix) =>
            categories.FirstOrDefault(c => c.Name.StartsWith(prefix))?.Id ?? 0;

        var vm = new HomeIndexVm
        {
            Categories = categories,
            Groups = groups,
            Discounted = products
                .Where(p => p.OldPrice.HasValue && p.OldPrice.Value > p.Price)
                .OrderByDescending(p => p.DiscountPercent).Take(6).ToList(),
            // A-5: mục Tin tức hiển thị bài thật từ DB, không còn viết cứng trong view.
            News = await _db.NewsPosts.Where(n => n.IsPublished)
                .OrderByDescending(n => n.PublishedAt).Take(4).ToListAsync(),
            Sections = new List<HomeSectionVm>()
        };

        // Định nghĩa các nhóm hiển thị trên trang chủ (theo thứ tự như trang tham khảo)
        var defs = new[]
        {
            new { Title = "CPU — Bộ vi xử lý", Icon = "bi-cpu", Cats = new[] { "CPU" }, Group = (string)null,
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 5 triệu", null, 5000000m), ("5 – 10 triệu", 5000000m, 10000000m), ("Trên 10 triệu", 10000000m, null) } },
            new { Title = "Mainboard — Bo mạch chủ", Icon = "bi-motherboard", Cats = new[] { "Mainboard" }, Group = (string)null,
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 3 triệu", null, 3000000m), ("3 – 7 triệu", 3000000m, 7000000m), ("Trên 7 triệu", 7000000m, null) } },
            new { Title = "Card đồ họa", Icon = "bi-gpu-card", Cats = new[] { "VGA" }, Group = (string)null,
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 7 triệu", null, 7000000m), ("7 – 15 triệu", 7000000m, 15000000m), ("Trên 15 triệu", 15000000m, null) } },
            new { Title = "RAM — Bộ nhớ trong", Icon = "bi-memory", Cats = new[] { "RAM" }, Group = (string)null,
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 1 triệu", null, 1000000m), ("1 – 3 triệu", 1000000m, 3000000m), ("Trên 3 triệu", 3000000m, null) } },
            // A-6: section gộp SSD+HDD thuộc nhóm "Lưu trữ" → link "Xem tất cả" dùng groupId.
            new { Title = "Ổ cứng SSD, HDD", Icon = "bi-device-hdd", Cats = new[] { "SSD", "HDD" }, Group = "Lưu trữ",
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 1 triệu", null, 1000000m), ("1 – 3 triệu", 1000000m, 3000000m), ("Trên 3 triệu", 3000000m, null) } },
            new { Title = "Màn hình", Icon = "bi-display", Cats = new[] { "Màn hình" }, Group = (string)null,
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 3 triệu", null, 3000000m), ("3 – 10 triệu", 3000000m, 10000000m), ("Trên 10 triệu", 10000000m, null) } },
            // A-6: section gộp PSU+Case thuộc nhóm "Nguồn - Vỏ - Tản nhiệt" → dùng groupId.
            new { Title = "Nguồn, vỏ case", Icon = "bi-plug", Cats = new[] { "PSU", "Case" }, Group = "Nguồn - Vỏ - Tản nhiệt",
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 2 triệu", null, 2000000m), ("2 – 4 triệu", 2000000m, 4000000m), ("Trên 4 triệu", 4000000m, null) } },
            new { Title = "Tản nhiệt, quạt", Icon = "bi-fan", Cats = new[] { "Tản nhiệt" }, Group = (string)null,
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 1 triệu", null, 1000000m), ("1 – 3 triệu", 1000000m, 3000000m), ("Trên 3 triệu", 3000000m, null) } },
            // A-6: section gộp Bàn phím+Chuột thuộc nhóm "Thiết bị ngoại vi" → dùng groupId.
            new { Title = "Gaming gear", Icon = "bi-headset", Cats = new[] { "Bàn phím", "Chuột" }, Group = "Thiết bị ngoại vi",
                Ranges = new (string, decimal?, decimal?)[]
                    { ("Dưới 500 nghìn", null, 500000m), ("500 nghìn – 2 triệu", 500000m, 2000000m), ("Trên 2 triệu", 2000000m, null) } },
        };

        foreach (var d in defs)
        {
            var ids = d.Cats.Select(CatId).Where(id => id != 0).ToList();
            var items = products.Where(p => ids.Contains(p.CategoryId))
                .OrderByDescending(p => p.CreatedAt).ThenByDescending(p => p.Id).ToList();
            if (!items.Any()) continue;
            vm.Sections.Add(new HomeSectionVm
            {
                Title = d.Title,
                Icon = d.Icon,
                CategoryIds = ids,
                // A-6: section gộp nhiều loại thì link theo nhóm (GroupId), section 1 loại link theo loại.
                GroupId = d.Group == null ? null : groups.FirstOrDefault(g => g.Name == d.Group)?.Id,
                Products = items.Take(6).ToList(),
                TotalCount = items.Count,
                Brands = items.Select(p => p.Brand).Where(b => b != null)
                              .DistinctBy(b => b.Id).OrderBy(b => b.Name).ToList(),
                PriceRanges = d.Ranges.Select(r => new PriceRangeVm
                    { Label = r.Item1, Min = r.Item2, Max = r.Item3 }).ToList()
            });
        }

        return View(vm);
    }

    // Trang thông tin tĩnh: giới thiệu, FAQ và các chính sách (dùng cho menu/footer)
    private static readonly string[] InfoTopics =
        { "gioi-thieu", "faq", "ban-hang", "doi-tra", "bao-hanh", "giao-hang", "thanh-toan", "bao-mat" };

    public IActionResult Info(string id)
    {
        if (string.IsNullOrEmpty(id) || !InfoTopics.Contains(id)) return NotFound();
        ViewData["Title"] = id switch
        {
            "gioi-thieu" => "Giới thiệu",
            "faq" => "Câu hỏi thường gặp",
            "ban-hang" => "Chính sách bán hàng",
            "doi-tra" => "Chính sách đổi trả",
            "bao-hanh" => "Chính sách bảo hành",
            "giao-hang" => "Chính sách giao hàng",
            "thanh-toan" => "Chính sách thanh toán",
            "bao-mat" => "Chính sách bảo mật",
            _ => "Thông tin"
        };
        return View((object)id);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        // B-2: hiển thị RequestId để đối chiếu với log khi báo lỗi.
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        _logger.LogWarning("Trang lỗi hiển thị, RequestId={RequestId}.", ViewData["RequestId"]);
        return View();
    }
}
