using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;
using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Helpers;

public static class SessionExtensions
{
    private const string CartKey = "CART";

    public static List<CartLine> GetCart(this ISession session)
    {
        var json = session.GetString(CartKey);
        return string.IsNullOrEmpty(json)
            ? new List<CartLine>()
            : JsonSerializer.Deserialize<List<CartLine>>(json) ?? new List<CartLine>();
    }

    public static void SaveCart(this ISession session, List<CartLine> cart) =>
        session.SetString(CartKey, JsonSerializer.Serialize(cart));

    public static void ClearCart(this ISession session) => session.Remove(CartKey);
}

public static class FormatExtensions
{
    private static readonly CultureInfo Vi = new CultureInfo("vi-VN");

    public static string Money(this decimal value) => value.ToString("N0", Vi) + " ₫";

    public static string Text(this OrderStatus s) => s switch
    {
        OrderStatus.Pending => "Chờ xác nhận",
        OrderStatus.Confirmed => "Đã xác nhận",
        OrderStatus.Shipping => "Đang giao hàng",
        OrderStatus.Completed => "Hoàn thành",
        OrderStatus.Cancelled => "Đã hủy",
        _ => s.ToString()
    };

    public static string Badge(this OrderStatus s) => s switch
    {
        OrderStatus.Pending => "bg-warning text-dark",
        OrderStatus.Confirmed => "bg-info text-dark",
        OrderStatus.Shipping => "bg-primary",
        OrderStatus.Completed => "bg-success",
        OrderStatus.Cancelled => "bg-secondary",
        _ => "bg-light text-dark"
    };

    public static int? CustomerId(this ClaimsPrincipal user) =>
        int.TryParse(user.FindFirstValue("CustomerId"), out var id) ? id : null;
}

public static class OrderHelper
{
    // Hủy đơn và hoàn lại tồn kho. Người gọi phải kiểm tra quyền/trạng thái trước.
    public static async Task CancelAsync(AppDbContext db, int orderId)
    {
        var order = await db.Orders
            .Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstAsync(o => o.Id == orderId);

        foreach (var d in order.Details)
            d.Product.Stock += d.Quantity;

        order.Status = OrderStatus.Cancelled;
        await db.SaveChangesAsync();
    }
}

/// <summary>
/// Máy trạng thái đơn hàng (A-2).
/// Pending → Confirmed → Shipping → Completed; từ Pending/Confirmed/Shipping → Cancelled.
/// Completed và Cancelled là trạng thái cuối, không chuyển đi đâu được nữa.
/// </summary>
public static class OrderTransitions
{
    private static readonly Dictionary<OrderStatus, OrderStatus[]> Allowed = new()
    {
        [OrderStatus.Pending] = new[] { OrderStatus.Confirmed, OrderStatus.Cancelled },
        [OrderStatus.Confirmed] = new[] { OrderStatus.Shipping, OrderStatus.Cancelled },
        [OrderStatus.Shipping] = new[] { OrderStatus.Completed, OrderStatus.Cancelled },
        [OrderStatus.Completed] = Array.Empty<OrderStatus>(),
        [OrderStatus.Cancelled] = Array.Empty<OrderStatus>()
    };

    public static IReadOnlyList<OrderStatus> AllowedTargets(OrderStatus from) => Allowed[from];

    public static bool IsAllowed(OrderStatus from, OrderStatus to) => Allowed[from].Contains(to);
}

/// <summary>
/// Kiểm tra tương thích Build PC bằng cách trích token từ trường Specifications (A-5).
/// Chỉ trích được khi thông số ghi rõ dạng "socket LGA1700", "socket AM5", "DDR5"...
/// Không trích được thì trả về null — người gọi phải báo "không xác định được",
/// tuyệt đối không đoán tương thích.
/// </summary>
public static class Compatibility
{
    private static readonly Regex SocketPattern = new(@"\b(LGA\d+|AM\d+)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex RamPattern = new(@"\b(DDR\d)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static string ExtractSocket(string specifications)
    {
        if (string.IsNullOrWhiteSpace(specifications)) return null;
        var m = SocketPattern.Match(specifications);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }

    public static string ExtractRamType(string specifications)
    {
        if (string.IsNullOrWhiteSpace(specifications)) return null;
        var m = RamPattern.Match(specifications);
        return m.Success ? m.Groups[1].Value.ToUpperInvariant() : null;
    }
}
