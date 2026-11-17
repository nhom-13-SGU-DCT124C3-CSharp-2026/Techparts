namespace ComputerStore.Helpers;

/// <summary>
/// Ảnh placeholder nội bộ cho sản phẩm (vòng 2, mục 5a): khi sản phẩm chưa có
/// ảnh thật, hoặc ảnh ngoài (picsum) không tải được vì mất mạng, giao diện hiển
/// thị SVG local theo nhóm linh kiện thay vì icon ảnh vỡ. Dùng ở _ProductCard,
/// Shop/Details và Cart/Index qua thuộc tính data-fallback + onerror.
/// </summary>
public static class ProductImageHelper
{
    /// <summary>Đường dẫn placeholder theo tên loại linh kiện (null/không khớp → mặc định).</summary>
    public static string PlaceholderFor(string categoryName)
    {
        if (string.IsNullOrWhiteSpace(categoryName)) return "/img/no-image.svg";
        var n = categoryName.Trim();
        if (n.StartsWith("CPU", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-cpu.svg";
        if (n.StartsWith("RAM", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-ram.svg";
        if (n.StartsWith("VGA", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-vga.svg";
        if (n.StartsWith("Mainboard", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-mainboard.svg";
        if (n.StartsWith("SSD", StringComparison.OrdinalIgnoreCase)
            || n.StartsWith("HDD", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-storage.svg";
        if (n.StartsWith("PSU", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-psu.svg";
        if (n.StartsWith("Màn hình", StringComparison.OrdinalIgnoreCase)) return "/img/placeholder-monitor.svg";
        return "/img/no-image.svg";
    }
}
