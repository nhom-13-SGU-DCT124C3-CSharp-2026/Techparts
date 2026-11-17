using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Mvc;

namespace ComputerStore.Controllers;

/// <summary>Form "Để lại số, chờ gọi lại" (A-4): lưu yêu cầu vào DB, admin xem/xử lý.</summary>
public class CallbackController : Controller
{
    private readonly AppDbContext _db;
    public CallbackController(AppDbContext db) => _db = db;

    [HttpGet]
    public IActionResult Create() => View(new CallbackRequest());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([Bind("Name,Phone,Note")] CallbackRequest m)
    {
        if (!ModelState.IsValid) return View(m);
        _db.CallbackRequests.Add(m);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Đã nhận số điện thoại của bạn. TechParts sẽ gọi lại trong giờ làm việc.";
        return RedirectToAction("Index", "Home");
    }
}
