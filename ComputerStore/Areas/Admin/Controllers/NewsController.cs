using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

/// <summary>CRUD tin tức phía admin (A-5).</summary>
[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class NewsController : Controller
{
    private const int PageSize = 20;
    private readonly AppDbContext _db;
    public NewsController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index(int page = 1)
    {
        var total = await _db.NewsPosts.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);
        ViewBag.Page = page; ViewBag.TotalPages = totalPages;
        var posts = await _db.NewsPosts.OrderByDescending(n => n.PublishedAt)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();
        return View(posts);
    }

    [HttpGet]
    public IActionResult Create() => View("Form", new NewsPost { PublishedAt = DateTime.Now });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Title,Summary,Content,ImageUrl,PublishedAt,IsPublished")] NewsPost model)
    {
        if (!ModelState.IsValid) return View("Form", model);
        _db.NewsPosts.Add(model);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã đăng bài viết.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var post = await _db.NewsPosts.FindAsync(id);
        return post == null ? NotFound() : View("Form", post);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, [Bind("Id,Title,Summary,Content,ImageUrl,PublishedAt,IsPublished")] NewsPost model)
    {
        if (id != model.Id) return BadRequest();
        if (!ModelState.IsValid) return View("Form", model);
        var post = await _db.NewsPosts.FindAsync(id);
        if (post == null) return NotFound();
        post.Title = model.Title; post.Summary = model.Summary; post.Content = model.Content;
        post.ImageUrl = model.ImageUrl; post.PublishedAt = model.PublishedAt; post.IsPublished = model.IsPublished;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã cập nhật bài viết.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var post = await _db.NewsPosts.FindAsync(id);
        if (post == null) return NotFound();
        _db.NewsPosts.Remove(post);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã xóa bài viết.";
        return RedirectToAction(nameof(Index));
    }
}
