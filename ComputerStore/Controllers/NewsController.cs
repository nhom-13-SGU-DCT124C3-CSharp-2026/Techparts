using ComputerStore.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Controllers;

/// <summary>Tin tức phía khách: danh sách + chi tiết bài đã xuất bản (A-5).</summary>
public class NewsController : Controller
{
    private readonly AppDbContext _db;
    public NewsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        var posts = await _db.NewsPosts.Where(n => n.IsPublished)
            .OrderByDescending(n => n.PublishedAt).ToListAsync();
        return View(posts);
    }

    public async Task<IActionResult> Details(int id)
    {
        var post = await _db.NewsPosts.FirstOrDefaultAsync(n => n.Id == id && n.IsPublished);
        return post == null ? NotFound() : View(post);
    }
}
