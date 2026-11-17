using ComputerStore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Components;

// Dùng cho footer: liệt kê các loại linh kiện (cần truy vấn DB nên không viết cứng trong layout)
public class FooterNavViewComponent : ViewComponent
{
    private readonly AppDbContext _db;
    public FooterNavViewComponent(AppDbContext db) => _db = db;

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var cats = await _db.Categories.OrderBy(c => c.Name).ToListAsync();
        return View(cats);
    }
}
