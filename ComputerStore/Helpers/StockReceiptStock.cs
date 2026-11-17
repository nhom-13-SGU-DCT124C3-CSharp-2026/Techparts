using ComputerStore.Models;

namespace ComputerStore.Helpers;

/// <summary>
/// Kiểm soát tồn kho khi sửa/xóa phiếu nhập kho (B-13).
/// Logic thuần (không chạm DB) để unit test được: mọi thay đổi tồn kho đều
/// được tính trước rồi mới kiểm tra — nếu bất kỳ sản phẩm nào bị trừ xuống
/// âm thì toàn bộ thao tác phải bị chặn, không lưu gì cả.
///
/// Quy ước dấu: <c>StockChange = số lượng mới - số lượng cũ</c> của một
/// sản phẩm trong phiếu (âm = đảo ngược / trừ kho, dương = nhập thêm).
/// Sửa phiếu: StockChange = tổng mới - tổng cũ theo từng sản phẩm.
/// Xóa phiếu: StockChange = -tổng số lượng đã nhập (đảo ngược toàn bộ).
/// </summary>
public static class StockReceiptStock
{
    /// <summary>Một sản phẩm bị chặn vì thao tác sẽ làm tồn kho của nó âm.</summary>
    public record BlockedProduct(
        int ProductId,
        string ProductName,
        int CurrentStock,
        int QuantityToReverse,
        int ResultingStock);

    /// <summary>Một thay đổi tồn kho dự kiến của một sản phẩm.</summary>
    public record StockChange(int ProductId, string ProductName, int CurrentStock, int Delta);

    /// <summary>
    /// Kiểm tra một tập thay đổi tồn kho. Trả về danh sách sản phẩm sẽ bị âm
    /// (rỗng = an toàn, được phép lưu). Sản phẩm có Delta &gt;= 0 không bao
    /// giờ bị chặn.
    /// </summary>
    public static List<BlockedProduct> Check(IEnumerable<StockChange> changes)
    {
        var blocked = new List<BlockedProduct>();
        foreach (var c in changes)
        {
            var resulting = c.CurrentStock + c.Delta;
            if (resulting < 0)
                blocked.Add(new BlockedProduct(
                    c.ProductId, c.ProductName, c.CurrentStock, -c.Delta, resulting));
        }
        return blocked;
    }

    /// <summary>
    /// Kiểm tra đảo ngược tồn kho (dùng khi xóa phiếu, hoặc đảo ngược toàn bộ
    /// số lượng cũ): <c>QuantityToReverse</c> là số chiếc sẽ bị trừ khỏi kho.
    /// </summary>
    public static List<BlockedProduct> CheckReversal(
        IEnumerable<(int ProductId, string ProductName, int CurrentStock, int QuantityToReverse)> reversals) =>
        Check(reversals.Select(r =>
            new StockChange(r.ProductId, r.ProductName, r.CurrentStock, -r.QuantityToReverse)));

    /// <summary>
    /// Tính thay đổi tồn kho ròng theo từng sản phẩm khi sửa phiếu:
    /// tổng số lượng mới trừ tổng số lượng cũ. Chỉ trả về sản phẩm có thay
    /// đổi khác 0. Dùng tổng theo sản phẩm nên tự xử lý được cả trường hợp
    /// thêm dòng, xóa dòng (dòng cũ bị bỏ = số lượng mới 0).
    /// Không đổi sản phẩm của dòng đã có (xem StockReceiptEditVm) nên mỗi
    /// sản phẩm chỉ xuất hiện ở một phía; nhưng hàm vẫn đúng cả khi gộp.
    /// </summary>
    public static Dictionary<int, int> NetDelta(
        IEnumerable<(int ProductId, int Quantity)> oldLines,
        IEnumerable<(int ProductId, int Quantity)> newLines)
    {
        var totals = new Dictionary<int, int>();
        foreach (var (id, qty) in oldLines)
            totals[id] = totals.GetValueOrDefault(id) - qty;
        foreach (var (id, qty) in newLines)
            totals[id] = totals.GetValueOrDefault(id) + qty;
        return totals.Where(kv => kv.Value != 0).ToDictionary(kv => kv.Key, kv => kv.Value);
    }
}
