using ComputerStore.Data;
using ComputerStore.Helpers;
using ComputerStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Services;

/// <summary>
/// Kiểm tra tính hợp lệ của mã voucher trước khi áp dụng (vòng 2).
/// Thứ tự kiểm tra: tồn tại → đang áp dụng → trong hạn → còn lượt → đủ đơn tối thiểu.
/// KHÔNG tăng UsedCount ở đây: UsedCount chỉ tăng khi đơn được tạo thành công,
/// trong cùng transaction tạo đơn ở CartController.Checkout (POST).
/// </summary>
public class VoucherService
{
    private readonly AppDbContext _db;
    public VoucherService(AppDbContext db) => _db = db;

    /// <returns>(Voucher đã validate, null) nếu hợp lệ; (null, lý do) nếu không.</returns>
    public async Task<(Voucher Voucher, string Error)> ValidateAsync(string code, decimal subtotal, DateTime now)
    {
        if (string.IsNullOrWhiteSpace(code))
            return (null, "Vui lòng nhập mã voucher.");

        var normalized = code.Trim().ToUpperInvariant();
        var voucher = await _db.Vouchers.FirstOrDefaultAsync(v => v.Code == normalized);
        if (voucher == null)
            return (null, $"Mã voucher \"{code.Trim()}\" không tồn tại.");
        if (!voucher.IsActive)
            return (null, "Mã voucher này đã bị vô hiệu hóa.");
        if (now < voucher.ValidFrom)
            return (null, $"Mã voucher chưa có hiệu lực (hiệu lực từ {voucher.ValidFrom:dd/MM/yyyy}).");
        if (now > voucher.ValidTo)
            return (null, "Mã voucher đã hết hạn.");
        if (voucher.UsedCount >= voucher.MaxUses)
            return (null, "Mã voucher đã hết lượt sử dụng.");
        if (subtotal < voucher.MinOrderAmount)
            return (null, $"Đơn hàng chưa đủ {voucher.MinOrderAmount.Money()} để áp dụng mã này.");
        return (voucher, null);
    }
}
