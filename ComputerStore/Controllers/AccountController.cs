using System.Security.Claims;
using ComputerStore.Data;
using ComputerStore.Helpers;
using ComputerStore.Models;
using ComputerStore.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Controllers;

public class AccountController : Controller
{
    // A-9: khóa tài khoản 15 phút sau 5 lần đăng nhập sai liên tiếp.
    private const int MaxFailedLogins = 5;
    private static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    private readonly AppDbContext _db;
    private readonly ILogger<AccountController> _logger;
    private readonly IEmailService _emailService;
    private readonly PasswordHasher<AppUser> _hasher = new();

    public AccountController(AppDbContext db, ILogger<AccountController> logger, IEmailService emailService)
    {
        _db = db;
        _logger = logger;
        _emailService = emailService;
    }

    private async Task SignIn(AppUser user, bool remember)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ClaimTypes.Name, user.DisplayName ?? user.Username),
            new(ClaimTypes.Role, user.Role)
        };
        if (user.CustomerId.HasValue) claims.Add(new Claim("CustomerId", user.CustomerId.Value.ToString()));

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
            new ClaimsPrincipal(identity), new AuthenticationProperties { IsPersistent = remember });
    }

    [HttpGet]
    public IActionResult Login(string returnUrl = null)
    {
        if (User.Identity.IsAuthenticated) return RedirectToAction("Index", "Home");
        return View(new LoginVm { ReturnUrl = returnUrl });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginVm m)
    {
        if (!ModelState.IsValid) return View(m);

        var user = await _db.Users.FirstOrDefaultAsync(u => u.Username == m.Username);

        // A-9: tài khoản đang bị khóa thì từ chối ngay, không kiểm tra mật khẩu nữa.
        if (user != null && user.LockoutUntil.HasValue && user.LockoutUntil.Value > DateTime.UtcNow)
        {
            _logger.LogWarning("Đăng nhập bị chặn: tài khoản {Username} đang bị khóa đến {Until}.", m.Username, user.LockoutUntil.Value);
            ModelState.AddModelError("", $"Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần. Vui lòng thử lại sau {user.LockoutUntil.Value.ToLocalTime():HH:mm}.");
            return View(m);
        }

        if (user == null ||
            _hasher.VerifyHashedPassword(user, user.PasswordHash, m.Password) == PasswordVerificationResult.Failed)
        {
            if (user != null)
            {
                user.FailedLoginAttempts++;
                if (user.FailedLoginAttempts >= MaxFailedLogins)
                {
                    user.LockoutUntil = DateTime.UtcNow.Add(LockoutDuration);
                    _logger.LogWarning("Tài khoản {Username} bị khóa 15 phút sau {Attempts} lần đăng nhập sai.", m.Username, user.FailedLoginAttempts);
                    await _db.SaveChangesAsync();
                    ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu. Tài khoản đã bị khóa 15 phút do đăng nhập sai nhiều lần.");
                    return View(m);
                }
                await _db.SaveChangesAsync();
                _logger.LogWarning("Đăng nhập sai lần {Attempts} cho tài khoản {Username}.", user.FailedLoginAttempts, m.Username);
            }
            ModelState.AddModelError("", "Sai tên đăng nhập hoặc mật khẩu");
            return View(m);
        }

        // Đăng nhập đúng: reset bộ đếm sai (A-9).
        if (user.FailedLoginAttempts > 0 || user.LockoutUntil.HasValue)
        {
            user.FailedLoginAttempts = 0;
            user.LockoutUntil = null;
            await _db.SaveChangesAsync();
        }

        await SignIn(user, m.RememberMe);

        if (!string.IsNullOrEmpty(m.ReturnUrl) && Url.IsLocalUrl(m.ReturnUrl)) return Redirect(m.ReturnUrl);
        return user.Role == Roles.Admin
            ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
            : RedirectToAction("Index", "Home");
    }

    [HttpGet]
    public IActionResult Register() => User.Identity.IsAuthenticated ? RedirectToAction("Index", "Home") : View(new RegisterVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(RegisterVm m)
    {
        if (await _db.Users.AnyAsync(u => u.Username == m.Username))
            ModelState.AddModelError(nameof(m.Username), "Tên đăng nhập đã tồn tại");
        // B-3: kiểm tra trùng Email và SĐT, không chỉ Username.
        if (!string.IsNullOrWhiteSpace(m.Email) && await _db.Customers.AnyAsync(c => c.Email == m.Email))
            ModelState.AddModelError(nameof(m.Email), "Email này đã được dùng để đăng ký");
        if (await _db.Customers.AnyAsync(c => c.Phone == m.Phone))
            ModelState.AddModelError(nameof(m.Phone), "Số điện thoại này đã được dùng để đăng ký");
        if (!ModelState.IsValid) return View(m);

        var customer = new Customer { FullName = m.FullName, Email = m.Email, Phone = m.Phone, Address = m.Address };
        var user = new AppUser { Username = m.Username, Role = Roles.Customer, DisplayName = m.FullName, Customer = customer };
        user.PasswordHash = _hasher.HashPassword(user, m.Password);

        _db.Users.Add(user);
        await _db.SaveChangesAsync();
        await SignIn(user, false);
        TempData["Success"] = "Đăng ký thành công. Chào mừng bạn!";
        return RedirectToAction("Index", "Home");
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied() => View();

    // ---------- Thông tin cá nhân ----------
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Profile()
    {
        var c = await _db.Customers.FindAsync(User.CustomerId());
        return c == null ? NotFound() : View(c);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Profile([Bind("FullName,Email,Phone,Address")] Customer m)
    {
        if (!ModelState.IsValid) return View(m);

        var c = await _db.Customers.FindAsync(User.CustomerId());
        if (c == null) return NotFound();

        // B-3: sửa thông tin cũng không được trùng Email/SĐT của khách khác.
        if (!string.IsNullOrWhiteSpace(m.Email) && await _db.Customers.AnyAsync(x => x.Email == m.Email && x.Id != c.Id))
            ModelState.AddModelError(nameof(m.Email), "Email này đã được dùng bởi khách hàng khác");
        if (await _db.Customers.AnyAsync(x => x.Phone == m.Phone && x.Id != c.Id))
            ModelState.AddModelError(nameof(m.Phone), "Số điện thoại này đã được dùng bởi khách hàng khác");
        if (!ModelState.IsValid) return View(m);

        c.FullName = m.FullName; c.Email = m.Email; c.Phone = m.Phone; c.Address = m.Address;

        // A-7: dùng FirstOrDefaultAsync + kiểm tra null thay vì FirstAsync (khách do
        // admin tạo tay có thể chưa có tài khoản đăng nhập).
        var user = await _db.Users.FirstOrDefaultAsync(u => u.CustomerId == c.Id);
        if (user == null) return NotFound();
        user.DisplayName = c.FullName;
        await _db.SaveChangesAsync();
        await SignIn(user, true);

        TempData["Success"] = "Đã cập nhật thông tin.";
        return RedirectToAction(nameof(Profile));
    }

    // ---------- Đơn hàng của tôi ----------
    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> Orders()
    {
        var cid = User.CustomerId();
        var orders = await _db.Orders.Include(o => o.PaymentMethod)
            .Where(o => o.CustomerId == cid).OrderByDescending(o => o.OrderDate).ToListAsync();
        return View(orders);
    }

    [Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> OrderDetail(int id)
    {
        var cid = User.CustomerId();
        var order = await _db.Orders.Include(o => o.PaymentMethod)
            .Include(o => o.Details).ThenInclude(d => d.Product)
            .FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == cid);
        return order == null ? NotFound() : View(order);
    }

    [HttpPost, ValidateAntiForgeryToken, Authorize(Roles = Roles.Customer)]
    public async Task<IActionResult> CancelOrder(int id)
    {
        var cid = User.CustomerId();
        var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id && o.CustomerId == cid);
        if (order == null) return NotFound();

        if (order.Status != OrderStatus.Pending)
            TempData["Error"] = "Chỉ có thể hủy đơn đang chờ xác nhận.";
        else
        {
            await OrderHelper.CancelAsync(_db, id);
            TempData["Success"] = "Đã hủy đơn hàng.";
        }
        return RedirectToAction(nameof(OrderDetail), new { id });
    }

    // ---------- Đổi mật khẩu (đã đăng nhập; áp dụng cho cả Admin và Customer) ----------
    [Authorize]
    public IActionResult ChangePassword() => View(new ChangePasswordVm());

    [HttpPost, ValidateAntiForgeryToken, Authorize]
    public async Task<IActionResult> ChangePassword(ChangePasswordVm m)
    {
        if (!ModelState.IsValid) return View(m);

        // Phòng thủ thêm ngoài DataAnnotations: cùng 1 quy tắc PasswordRules.
        if (!PasswordRules.IsStrong(m.NewPassword))
        {
            ModelState.AddModelError(nameof(m.NewPassword), PasswordRules.ErrorMessage);
            return View(m);
        }
        if (m.NewPassword == m.CurrentPassword)
        {
            ModelState.AddModelError(nameof(m.NewPassword), "Mật khẩu mới phải khác mật khẩu hiện tại.");
            return View(m);
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return RedirectToAction(nameof(Login));
        var user = await _db.Users.FindAsync(userId);
        if (user == null) return RedirectToAction(nameof(Login));

        if (_hasher.VerifyHashedPassword(user, user.PasswordHash, m.CurrentPassword)
                == PasswordVerificationResult.Failed)
        {
            ModelState.AddModelError(nameof(m.CurrentPassword), "Mật khẩu hiện tại không đúng.");
            return View(m);
        }

        user.PasswordHash = _hasher.HashPassword(user, m.NewPassword);
        // Đổi mật khẩu thành công thì gỡ khóa đăng nhập sai (nếu có) như khi đăng nhập đúng (A-9).
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;
        await _db.SaveChangesAsync();
        _logger.LogInformation("Tài khoản {Username} đã đổi mật khẩu.", user.Username);

        TempData["Success"] = "Đã đổi mật khẩu thành công.";
        return user.Role == Roles.Admin
            ? RedirectToAction("Index", "Dashboard", new { area = "Admin" })
            : RedirectToAction(nameof(Profile));
    }

    // ---------- Quên mật khẩu (token reset qua email) ----------
    // Chống dò tài khoản: dù email có tồn tại hay không cũng hiện cùng 1 thông báo.
    // Tài khoản admin không có Customer nên không có email — luồng này chỉ áp dụng
    // cho tài khoản khách hàng có email; admin quên mật khẩu phải nhờ quản trị DB
    // đặt lại trực tiếp (ghi rõ trong FIX_LOG_2_PASSWORD.md, không bịa email cho admin).
    [HttpGet]
    public IActionResult ForgotPassword() => View(new ForgotPasswordVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordVm m)
    {
        const string genericMessage = "Nếu email tồn tại trong hệ thống, link đặt lại mật khẩu đã được gửi. Vui lòng kiểm tra hộp thư (kể cả thư rác).";
        if (!ModelState.IsValid) return View(m);

        var email = m.Email.Trim();
        var customer = await _db.Customers
            .FirstOrDefaultAsync(c => c.Email != null && c.Email.ToLower() == email.ToLower());
        var user = customer == null
            ? null
            : await _db.Users.FirstOrDefaultAsync(u => u.CustomerId == customer.Id);

        if (user != null)
        {
            var (token, entity) = PasswordResetToken.Generate(user.Id);
            _db.PasswordResetTokens.Add(entity);
            await _db.SaveChangesAsync();

            var resetLink = Url.Action(nameof(ResetPassword), "Account",
                new { token, userId = user.Id }, Request.Scheme);
            try
            {
                await _emailService.SendPasswordResetAsync(email, resetLink);
            }
            catch (Exception ex)
            {
                // Gửi mail lỗi (vd: chưa cấu hình SMTP) cũng không được lộ ra ngoài:
                // người dùng vẫn chỉ thấy thông báo chung; lỗi ghi vào log để admin xử lý.
                _logger.LogError(ex, "Không gửi được mail đặt lại mật khẩu cho {Email}.", email);
            }
            _logger.LogInformation("Đã tạo token đặt lại mật khẩu cho tài khoản {Username}.", user.Username);
        }
        else
        {
            _logger.LogInformation("Yêu cầu quên mật khẩu với email không gắn tài khoản: {Email}.", email);
        }

        TempData["Success"] = genericMessage;
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> ResetPassword(string token, int userId)
    {
        var reset = await FindUsableTokenAsync(token, userId);
        if (reset == null)
        {
            TempData["Error"] = "Link đặt lại mật khẩu không hợp lệ, đã hết hạn hoặc đã được sử dụng. Vui lòng yêu cầu link mới.";
            return RedirectToAction(nameof(ForgotPassword));
        }
        return View(new ResetPasswordVm { Token = token, UserId = userId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordVm m)
    {
        if (!ModelState.IsValid) return View(m);
        if (!PasswordRules.IsStrong(m.NewPassword))
        {
            ModelState.AddModelError(nameof(m.NewPassword), PasswordRules.ErrorMessage);
            return View(m);
        }

        var reset = await FindUsableTokenAsync(m.Token, m.UserId);
        if (reset == null)
        {
            TempData["Error"] = "Link đặt lại mật khẩu không hợp lệ, đã hết hạn hoặc đã được sử dụng. Vui lòng yêu cầu link mới.";
            return RedirectToAction(nameof(ForgotPassword));
        }

        var user = await _db.Users.FindAsync(m.UserId);
        if (user == null) return NotFound();

        user.PasswordHash = _hasher.HashPassword(user, m.NewPassword);
        user.FailedLoginAttempts = 0;
        user.LockoutUntil = null;

        // Token dùng 1 lần: đánh dấu token này đã dùng + vô hiệu hóa mọi token
        // còn lại của user đó (kể cả token vừa tạo trước đó nhưng chưa dùng).
        var now = DateTime.UtcNow;
        reset.MarkUsed(now);
        var others = await _db.PasswordResetTokens
            .Where(t => t.UserId == user.Id && t.UsedAt == null && t.Id != reset.Id)
            .ToListAsync();
        foreach (var t in others) t.MarkUsed(now);

        await _db.SaveChangesAsync();
        _logger.LogInformation("Tài khoản {Username} đã đặt lại mật khẩu bằng token.", user.Username);

        TempData["Success"] = "Đã đặt lại mật khẩu thành công. Vui lòng đăng nhập bằng mật khẩu mới.";
        return RedirectToAction(nameof(Login));
    }

    /// <summary>
    /// Tìm token reset còn dùng được của user: chưa dùng, chưa hết hạn, hash khớp.
    /// Token chỉ lưu dạng hash nên phải tải các token chưa dùng của user rồi so hash
    /// trong bộ nhớ (số token chưa dùng của 1 user luôn rất ít).
    /// </summary>
    private async Task<PasswordResetToken> FindUsableTokenAsync(string token, int userId)
    {
        if (string.IsNullOrEmpty(token) || userId <= 0) return null;
        var candidates = await _db.PasswordResetTokens
            .Where(t => t.UserId == userId && t.UsedAt == null)
            .ToListAsync();
        var now = DateTime.UtcNow;
        return candidates.FirstOrDefault(t => t.IsUsable(now) && t.Matches(token));
    }
}
