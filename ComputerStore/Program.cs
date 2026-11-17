using ComputerStore.Data;
using ComputerStore.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddSession(o =>
{
    o.IdleTimeout = TimeSpan.FromHours(2);
    o.Cookie.HttpOnly = true;
    o.Cookie.IsEssential = true;
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(o =>
    {
        o.LoginPath = "/Account/Login";
        o.AccessDeniedPath = "/Account/AccessDenied";
        o.ExpireTimeSpan = TimeSpan.FromHours(8);
        o.SlidingExpiration = true;
    });
builder.Services.AddAuthorization();

// Vòng 2 — đăng ký các service mới:
// - CartService: giỏ hàng bền (DB cho khách đã đăng nhập).
// - OrderPricingService, VoucherService: phí ship + voucher (logic thuần).
builder.Services.AddScoped<CartService>();
builder.Services.AddScoped<OrderPricingService>();
builder.Services.AddScoped<VoucherService>();
// IEmailService: có Smtp:Host thì gửi mail thật; chưa cấu hình + Development thì
// chỉ ghi link reset vào log; chưa cấu hình + Production thì ném lỗi (xem EmailServiceFactory).
builder.Services.AddSingleton<IEmailService>(sp =>
    EmailServiceFactory.Create(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IWebHostEnvironment>(),
        sp.GetRequiredService<ILoggerFactory>()));

var app = builder.Build();

// Tạo database (nếu chưa có) và nạp dữ liệu mẫu (B-2: log lỗi seed thay vì crash lặng)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    try
    {
        DbSeeder.Seed(db);
    }
    catch (Exception ex)
    {
        app.Logger.LogError(ex, "Lỗi khi seed dữ liệu mẫu. Ứng dụng vẫn chạy nhưng dữ liệu mẫu có thể thiếu.");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "areas",
    pattern: "{area:exists}/{controller=Dashboard}/{action=Index}/{id?}");

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
