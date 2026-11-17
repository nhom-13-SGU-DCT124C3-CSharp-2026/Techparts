using ComputerStore.Models;

namespace ComputerStore.Services;

/// <summary>Kết quả tính giá một đơn hàng (chưa gồm thuế — dự án không tính VAT).</summary>
public record PricingResult(decimal Subtotal, decimal ShippingFee, decimal DiscountAmount, decimal GrandTotal);

/// <summary>
/// Tính phí ship + giảm giá + tổng cuối. Logic PURE (không chạm DB, không đọc
/// cấu hình) để unit test được trực tiếp.
/// Quy tắc:
/// - Phí ship: lấy quy tắc đang áp dụng (IsActive) có SortOrder nhỏ nhất;
///   tạm tính &gt;= FreeFromAmount thì miễn phí, ngược lại tính Fee.
///   Không có quy tắc nào đang áp dụng thì phí = 0.
/// - Voucher Percent: giảm % trên tạm tính, kẹp bởi MaxDiscountAmount (nếu có).
/// - Voucher Fixed: giảm số tiền cố định, không vượt tạm tính.
/// - GrandTotal = Subtotal + ShippingFee - DiscountAmount, không âm.
/// Voucher truyền vào được coi là ĐÃ validate (qua VoucherService).
/// </summary>
public class OrderPricingService
{
    public PricingResult Calculate(decimal subtotal, IEnumerable<ShippingRule> rules, Voucher voucher)
    {
        var rule = rules?
            .Where(r => r.IsActive)
            .OrderBy(r => r.SortOrder)
            .ThenBy(r => r.Id)
            .FirstOrDefault();

        decimal shippingFee = 0;
        if (rule != null)
            shippingFee = subtotal >= rule.FreeFromAmount ? 0 : rule.Fee;

        decimal discount = 0;
        if (voucher != null)
        {
            if (voucher.DiscountType == DiscountType.Percent)
            {
                discount = Math.Round(subtotal * voucher.DiscountValue / 100m, 0,
                    MidpointRounding.AwayFromZero);
                if (voucher.MaxDiscountAmount.HasValue)
                    discount = Math.Min(discount, voucher.MaxDiscountAmount.Value);
            }
            else
            {
                discount = voucher.DiscountValue;
            }
            discount = Math.Min(discount, subtotal);
        }

        var grandTotal = Math.Max(0, subtotal + shippingFee - discount);
        return new PricingResult(subtotal, shippingFee, discount, grandTotal);
    }
}
