using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Roles = Roles.Admin)]
public class DashboardController : Controller
{
    private readonly AppDbContext _db;
    public DashboardController(AppDbContext db) => _db = db;

    public async Task<IActionResult> Index()
    {
        // Doanh thu: lấy danh sách tổng tiền rồi cộng ở client — SumAsync trên cột
        // decimal không chạy được trên SQLite (môi trường chạy thử), SQL Server thì được.
        var completedTotals = await _db.Orders.Where(o => o.Status == OrderStatus.Completed)
            .Select(o => o.Total).ToListAsync();
        var vm = new DashboardVm
        {
            ProductCount = await _db.Products.CountAsync(),
            CustomerCount = await _db.Customers.CountAsync(),
            OrderCount = await _db.Orders.CountAsync(),
            PendingOrders = await _db.Orders.CountAsync(o => o.Status == OrderStatus.Pending),
            Revenue = completedTotals.Sum(),
            LowStock = await _db.Products.Include(p => p.Category)
                                .Where(p => p.IsActive && p.Stock <= 5)
                                .OrderBy(p => p.Stock).Take(8).ToListAsync(),
            RecentOrders = await _db.Orders.Include(o => o.Customer)
                                .OrderByDescending(o => o.OrderDate).Take(8).ToListAsync()
        };
        return View(vm);
    }
}
