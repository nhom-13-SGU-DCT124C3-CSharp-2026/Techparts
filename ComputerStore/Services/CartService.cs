using ComputerStore.Data;
using ComputerStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Services;

/// <summary>
/// Logic gộp giỏ thuần túy, tách riêng để unit test được mà không cần DB.
/// </summary>
public static class CartMerge
{
    public sealed record MergeResult(
        List<CartLine> Lines,
        List<int> ClampedProductIds,
        List<int> DroppedProductIds,
        List<int> MissingProductIds);

    /// <summary>
    /// Gộp giỏ session với giỏ DB: cộng số lượng theo từng sản phẩm rồi kẹp
    /// theo tồn kho hiện tại.
    /// </summary>
    /// <param name="sessionLines">Các dòng trong giỏ session (khách vãng lai).</param>
    /// <param name="dbLines">Các dòng đã lưu trong DB của khách.</param>
    /// <param name="stocks">
    /// Tồn kho hiện tại theo ProductId. Chỉ sản phẩm còn bán (IsActive) mới có
    /// mặt trong từ điển này — sản phẩm vắng mặt coi như đã ngừng bán/không
    /// còn tồn tại và bị bỏ khỏi giỏ gộp.
    /// </param>
    /// <returns>
    /// Các dòng sau khi gộp, kèm danh sách sản phẩm bị kẹp số lượng
    /// (<see cref="MergeResult.ClampedProductIds"/>), bị bỏ vì hết hàng
    /// (<see cref="MergeResult.DroppedProductIds"/>) và bị bỏ vì ngừng bán
    /// (<see cref="MergeResult.MissingProductIds"/>).
    /// </returns>
    public static MergeResult Merge(
        IEnumerable<CartLine> sessionLines,
        IEnumerable<CartLine> dbLines,
        IReadOnlyDictionary<int, int> stocks)
    {
        var totals = new Dictionary<int, int>();

        void Add(IEnumerable<CartLine> lines)
        {
            foreach (var line in lines ?? Enumerable.Empty<CartLine>())
            {
                if (line == null || line.Quantity <= 0) continue;
                totals[line.ProductId] = totals.TryGetValue(line.ProductId, out var q)
                    ? q + line.Quantity
                    : line.Quantity;
            }
        }

        Add(sessionLines);
        Add(dbLines);

        var lines = new List<CartLine>();
        var clamped = new List<int>();
        var dropped = new List<int>();
        var missing = new List<int>();

        foreach (var (productId, qty) in totals.OrderBy(kv => kv.Key))
        {
            if (!stocks.TryGetValue(productId, out var stock))
            {
                missing.Add(productId);
                continue;
            }
            if (stock <= 0)
            {
                dropped.Add(productId);
                continue;
            }
            if (qty > stock)
            {
                lines.Add(new CartLine { ProductId = productId, Quantity = stock });
                clamped.Add(productId);
            }
            else
            {
                lines.Add(new CartLine { ProductId = productId, Quantity = qty });
            }
        }

        return new MergeResult(lines, clamped, dropped, missing);
    }
}

/// <summary>
/// Giỏ hàng bền (vòng 2): khách đã đăng nhập thì giỏ lưu trong bảng
/// CartItems, không mất khi hết session hay đăng xuất; khách vãng lai vẫn
/// dùng giỏ session như cũ (xem CartController — service này chỉ làm việc
/// với DB, controller mới là nơi rẽ nhánh session/DB).
///
/// LAZY MERGE (gộp giỏ khi đăng nhập): KHÔNG chặn lúc đăng nhập và KHÔNG sửa
/// AccountController. Lần đầu tiên sau khi đăng nhập mà khách mở giỏ, thêm,
/// sửa hay xóa món, controller gọi
/// <see cref="MergeSessionCartAsync"/> (trực tiếp) hoặc
/// <see cref="GetItemsAsync"/> (khi dựng giỏ) để gộp giỏ session (nếu có)
/// vào giỏ DB: cộng số lượng theo từng sản phẩm, kẹp theo tồn kho hiện tại,
/// rồi controller xóa giỏ session nên lần sau không gộp lại nữa. Gộp xong,
/// mọi thao tác tiếp theo của khách đó đều đọc/ghi thẳng bảng CartItems.
///
/// Mọi thao tác số lượng đều kẹp theo tồn kho phía server như A-8 (không
/// tin client). Service không dùng ExecuteUpdateAsync/ExecuteDeleteAsync để
/// vẫn test được trên EF Core InMemory (xem TEST_LOG vòng 1).
///
/// ĐĂNG KÝ DI (do điều phối viên gắn vào Program.cs — file này không sửa
/// Program.cs): builder.Services.AddScoped&lt;CartService&gt;();
/// </summary>
public class CartService
{
    private readonly AppDbContext _db;

    public CartService(AppDbContext db) => _db = db;

    /// <summary>Kết quả tải giỏ: các dòng hiệu lực + thông báo phát sinh khi gộp.</summary>
    public sealed record CartLoadResult(List<CartLine> Lines, List<string> MergeNotices);

    /// <summary>
    /// Lấy các dòng giỏ hiệu lực của người dùng hiện tại.
    /// Khách vãng lai (<paramref name="customerId"/> null): trả nguyên giỏ
    /// session. Khách đã đăng nhập: gộp giỏ session (nếu có) vào DB rồi trả
    /// các dòng trong DB. Controller phải xóa giỏ session sau khi gọi hàm này
    /// nếu <paramref name="sessionLines"/> không rỗng (service không chạm
    /// vào HttpContext/Session để còn unit test được).
    /// </summary>
    public async Task<CartLoadResult> GetItemsAsync(int? customerId, List<CartLine> sessionLines)
    {
        sessionLines ??= new List<CartLine>();
        if (!customerId.HasValue)
            return new CartLoadResult(sessionLines, new List<string>());

        if (sessionLines.Count == 0)
            return new CartLoadResult(await GetLinesAsync(customerId.Value), new List<string>());

        var merged = await MergeSessionCartAsync(customerId.Value, sessionLines);
        return new CartLoadResult(merged.Lines, merged.Notices);
    }

    /// <summary>Các dòng giỏ đã lưu trong DB của một khách (chưa kẹp theo tồn kho).</summary>
    public async Task<List<CartLine>> GetLinesAsync(int customerId) =>
        await _db.CartItems
            .Where(c => c.CustomerId == customerId)
            .Select(c => new CartLine { ProductId = c.ProductId, Quantity = c.Quantity })
            .ToListAsync();

    public enum CartAddOutcome { Added, NotFound, OutOfStock }

    public sealed record CartAddResult(CartAddOutcome Outcome, int Quantity);

    /// <summary>
    /// Thêm món vào giỏ DB. Số lượng cộng thêm được kẹp theo tồn kho (A-8):
    /// món mới kẹp về [1, Stock], món đã có cộng dồn rồi kẹp về Stock.
    /// </summary>
    public async Task<CartAddResult> AddAsync(int customerId, int productId, int qty)
    {
        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null) return new CartAddResult(CartAddOutcome.NotFound, 0);
        if (product.Stock <= 0) return new CartAddResult(CartAddOutcome.OutOfStock, 0);

        var add = Math.Clamp(qty, 1, product.Stock);
        var row = await _db.CartItems.FirstOrDefaultAsync(
            c => c.CustomerId == customerId && c.ProductId == productId);
        if (row == null)
        {
            _db.CartItems.Add(new CartItem
            {
                CustomerId = customerId,
                ProductId = productId,
                Quantity = add,
                UpdatedAt = DateTime.UtcNow
            });
        }
        else
        {
            row.Quantity = Math.Min(row.Quantity + add, product.Stock);
            row.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync();
        return new CartAddResult(CartAddOutcome.Added, add);
    }

    public enum CartUpdateOutcome { Updated, Clamped, Removed, NotFound, OutOfStock }

    public sealed record CartUpdateResult(
        CartUpdateOutcome Outcome, int Quantity, int Stock, string ProductName);

    /// <summary>
    /// Đổi số lượng một dòng trong giỏ DB, kẹp theo tồn kho phía server (A-8).
    /// qty &lt;= 0: bỏ dòng. Sản phẩm hết hàng/ngừng bán: bỏ dòng, báo OutOfStock.
    /// qty &gt; Stock: kẹp về Stock, báo Clamped.
    /// </summary>
    public async Task<CartUpdateResult> UpdateAsync(int customerId, int productId, int qty)
    {
        var row = await _db.CartItems.FirstOrDefaultAsync(
            c => c.CustomerId == customerId && c.ProductId == productId);
        if (row == null)
            return new CartUpdateResult(CartUpdateOutcome.NotFound, 0, 0, null);

        if (qty <= 0)
        {
            _db.CartItems.Remove(row);
            await _db.SaveChangesAsync();
            return new CartUpdateResult(CartUpdateOutcome.Removed, 0, 0, null);
        }

        var product = await _db.Products
            .FirstOrDefaultAsync(p => p.Id == productId && p.IsActive);
        if (product == null || product.Stock <= 0)
        {
            _db.CartItems.Remove(row);
            await _db.SaveChangesAsync();
            return new CartUpdateResult(
                CartUpdateOutcome.OutOfStock, 0, 0, product?.Name);
        }

        if (qty > product.Stock)
        {
            row.Quantity = product.Stock;
            row.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            return new CartUpdateResult(
                CartUpdateOutcome.Clamped, product.Stock, product.Stock, product.Name);
        }

        row.Quantity = qty;
        row.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return new CartUpdateResult(
            CartUpdateOutcome.Updated, qty, product.Stock, product.Name);
    }

    /// <summary>Bỏ một món khỏi giỏ DB (không có thì thôi, không báo lỗi).</summary>
    public async Task RemoveAsync(int customerId, int productId)
    {
        var row = await _db.CartItems.FirstOrDefaultAsync(
            c => c.CustomerId == customerId && c.ProductId == productId);
        if (row == null) return;
        _db.CartItems.Remove(row);
        await _db.SaveChangesAsync();
    }

    /// <summary>Xóa toàn bộ giỏ DB của một khách (dùng sau khi checkout thành công).</summary>
    public async Task ClearAsync(int customerId)
    {
        var rows = await _db.CartItems
            .Where(c => c.CustomerId == customerId).ToListAsync();
        if (rows.Count == 0) return;
        _db.CartItems.RemoveRange(rows);
        await _db.SaveChangesAsync();
    }

    /// <summary>Kết quả gộp giỏ session vào DB: các dòng sau gộp + thông báo cho khách.</summary>
    public sealed record CartMergeOutcome(List<CartLine> Lines, List<string> Notices);

    /// <summary>
    /// Gộp giỏ session vào giỏ DB của khách (lazy merge — xem ghi chú ở đầu
    /// class). Cộng số lượng theo từng sản phẩm, kẹp theo tồn kho hiện tại,
    /// bỏ món hết hàng/ngừng bán, rồi ghi đè giỏ DB bằng kết quả gộp.
    /// Controller phải xóa giỏ session sau khi gọi hàm này.
    /// </summary>
    public async Task<CartMergeOutcome> MergeSessionCartAsync(
        int customerId, IEnumerable<CartLine> sessionLines)
    {
        var session = (sessionLines ?? Enumerable.Empty<CartLine>()).ToList();
        var dbLines = await GetLinesAsync(customerId);

        var ids = session.Concat(dbLines).Select(l => l.ProductId).Distinct().ToList();
        var products = await _db.Products
            .Where(p => ids.Contains(p.Id) && p.IsActive)
            .Select(p => new { p.Id, p.Name, p.Stock })
            .ToListAsync();
        var stocks = products.ToDictionary(p => p.Id, p => p.Stock);
        var names = products.ToDictionary(p => p.Id, p => p.Name);

        var result = CartMerge.Merge(session, dbLines, stocks);

        // Ghi đè giỏ DB bằng kết quả gộp (xóa dòng không còn, cập nhật dòng
        // còn lại, thêm dòng mới từ session).
        var existing = await _db.CartItems
            .Where(c => c.CustomerId == customerId).ToListAsync();
        var wanted = result.Lines.ToDictionary(l => l.ProductId, l => l.Quantity);
        _db.CartItems.RemoveRange(existing.Where(r => !wanted.ContainsKey(r.ProductId)));
        foreach (var row in existing.Where(r => wanted.ContainsKey(r.ProductId)))
        {
            row.Quantity = wanted[row.ProductId];
            row.UpdatedAt = DateTime.UtcNow;
        }
        foreach (var line in result.Lines.Where(l => existing.All(r => r.ProductId != l.ProductId)))
        {
            _db.CartItems.Add(new CartItem
            {
                CustomerId = customerId,
                ProductId = line.ProductId,
                Quantity = line.Quantity,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();

        var notices = new List<string>();
        foreach (var pid in result.ClampedProductIds)
            notices.Add($"\"{names[pid]}\" chỉ còn {stocks[pid]} chiếc nên số lượng đã giảm xuống {stocks[pid]}.");
        foreach (var pid in result.DroppedProductIds)
            notices.Add($"\"{names[pid]}\" đã hết hàng nên đã được bỏ khỏi giỏ.");
        foreach (var pid in result.MissingProductIds)
            notices.Add("Một sản phẩm trong giỏ đã ngừng bán hoặc không còn tồn tại nên đã được bỏ khỏi giỏ.");
        return new CartMergeOutcome(result.Lines, notices);
    }

    /// <summary>
    /// Tổng số lượng món trong giỏ (dùng cho badge ở header). CHỈ ĐỌC, không gộp
    /// và không ghi DB: khách đã đăng nhập = tổng DB + tổng session (hai nhóm này
    /// chưa gộp thì rời nhau nên cộng lại đúng bằng tổng sau gộp; sau khi gộp
    /// session rỗng nên chỉ còn tổng DB). Không gọi GetItemsAsync ở header vì nó
    /// gộp (ghi DB) mà layout không xóa session → gộp lặp sẽ cộng dồn sai.
    /// </summary>
    public async Task<int> GetCountAsync(int? customerId, List<CartLine> sessionLines)
    {
        var sessionCount = (sessionLines ?? new List<CartLine>()).Sum(l => l.Quantity);
        if (!customerId.HasValue) return sessionCount;
        var dbCount = (await GetLinesAsync(customerId.Value)).Sum(l => l.Quantity);
        return dbCount + sessionCount;
    }

    /// <summary>
    /// Ghi đè giỏ DB bằng danh sách dòng đã điều chỉnh (dùng trong BuildItems
    /// để lưu lại các dòng bị kẹp/bỏ theo tồn kho hiện tại, tương đương
    /// SaveCart() của giỏ session trước đây).
    /// </summary>
    public async Task SaveLinesAsync(int customerId, IEnumerable<CartLine> lines)
    {
        var wanted = (lines ?? Enumerable.Empty<CartLine>())
            .Where(l => l != null && l.Quantity > 0)
            .GroupBy(l => l.ProductId)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Quantity));

        var existing = await _db.CartItems
            .Where(c => c.CustomerId == customerId).ToListAsync();
        _db.CartItems.RemoveRange(existing.Where(r => !wanted.ContainsKey(r.ProductId)));
        foreach (var row in existing.Where(r => wanted.ContainsKey(r.ProductId)))
        {
            row.Quantity = wanted[row.ProductId];
            row.UpdatedAt = DateTime.UtcNow;
        }
        foreach (var kv in wanted.Where(kv => existing.All(r => r.ProductId != kv.Key)))
        {
            _db.CartItems.Add(new CartItem
            {
                CustomerId = customerId,
                ProductId = kv.Key,
                Quantity = kv.Value,
                UpdatedAt = DateTime.UtcNow
            });
        }
        await _db.SaveChangesAsync();
    }
}
