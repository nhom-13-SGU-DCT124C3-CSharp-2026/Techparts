using System.Security.Cryptography;
using System.Text;

namespace ComputerStore.Models;

/// <summary>
/// Token đặt lại mật khẩu (quên mật khẩu).
/// CHỈ lưu dạng hash SHA256 của token — không bao giờ lưu token thô trong DB.
/// Token hết hạn sau 1 giờ và chỉ dùng được 1 lần (UsedAt != null là đã dùng).
/// </summary>
public class PasswordResetToken
{
    public int Id { get; set; }

    /// <summary>Id của AppUser sở hữu token.</summary>
    public int UserId { get; set; }

    /// <summary>SHA256 hex (chữ thường) của token gửi trong link. Không lưu token thô.</summary>
    public string TokenHash { get; set; }

    public DateTime ExpiresAt { get; set; }

    /// <summary>Null = chưa dùng; có giá trị = đã dùng (thời điểm dùng).</summary>
    public DateTime? UsedAt { get; set; }

    public DateTime CreatedAt { get; set; }

    /// <summary>Thời hạn của token: 1 giờ kể từ lúc tạo.</summary>
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    /// <summary>
    /// Tạo token mới: sinh 32 byte ngẫu nhiên, mã hóa Base64Url để đưa vào link,
    /// chỉ lưu hash SHA256 của chuỗi token đó.
    /// Trả về (token thô để gửi qua mail, entity đã điền sẵn để lưu DB).
    /// </summary>
    public static (string Token, PasswordResetToken Entity) Generate(int userId, DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        var bytes = RandomNumberGenerator.GetBytes(32);
        var token = Convert.ToBase64String(bytes)
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
        var entity = new PasswordResetToken
        {
            UserId = userId,
            TokenHash = ComputeHash(token),
            ExpiresAt = now.Add(Lifetime),
            UsedAt = null,
            CreatedAt = now
        };
        return (token, entity);
    }

    /// <summary>SHA256 hex (chữ thường) của token. Dùng để so sánh, không lưu token thô.</summary>
    public static string ComputeHash(string token)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Kiểm tra token người dùng gửi lên có khớp hash đã lưu không.
    /// So sánh hằng thời gian (FixedTimeEquals) để tránh rò rỉ qua timing.
    /// </summary>
    public bool Matches(string token)
    {
        if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(TokenHash)) return false;
        var expected = Convert.FromHexString(TokenHash);
        var actual = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(expected, actual);
    }

    /// <summary>Token còn dùng được khi chưa dùng (UsedAt == null) và chưa hết hạn.</summary>
    public bool IsUsable(DateTime? utcNow = null) =>
        UsedAt == null && ExpiresAt > (utcNow ?? DateTime.UtcNow);

    /// <summary>Đánh dấu token đã dùng (dùng 1 lần).</summary>
    public void MarkUsed(DateTime? utcNow = null) => UsedAt = utcNow ?? DateTime.UtcNow;
}
