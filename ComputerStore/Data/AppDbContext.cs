using ComputerStore.Models;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<AppUser> Users => Set<AppUser>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<ProductGroup> ProductGroups => Set<ProductGroup>();
    public DbSet<Brand> Brands => Set<Brand>();
    public DbSet<PaymentMethod> PaymentMethods => Set<PaymentMethod>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderDetail> OrderDetails => Set<OrderDetail>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptDetail> StockReceiptDetails => Set<StockReceiptDetail>();
    public DbSet<CallbackRequest> CallbackRequests => Set<CallbackRequest>();
    public DbSet<NewsPost> NewsPosts => Set<NewsPost>();
    public DbSet<CartItem> CartItems => Set<CartItem>(); // Giỏ hàng bền, vòng 2.
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>(); // Vòng 2.
    public DbSet<ShippingRule> ShippingRules => Set<ShippingRule>(); // Vòng 2.
    public DbSet<Voucher> Vouchers => Set<Voucher>(); // Vòng 2.

    protected override void OnModelCreating(ModelBuilder mb)
    {
        base.OnModelCreating(mb);

        // Không cho xóa dây chuyền: dữ liệu đã phát sinh giao dịch phải được bảo vệ
        foreach (var fk in mb.Model.GetEntityTypes().SelectMany(e => e.GetForeignKeys()))
            fk.DeleteBehavior = DeleteBehavior.Restrict;

        mb.Entity<AppUser>().HasIndex(u => u.Username).IsUnique();
        mb.Entity<Product>().HasIndex(p => p.Sku).IsUnique();
        mb.Entity<Category>().HasIndex(x => x.Name).IsUnique();
        mb.Entity<ProductGroup>().HasIndex(x => x.Name).IsUnique();
        mb.Entity<Brand>().HasIndex(x => x.Name).IsUnique();
        mb.Entity<PaymentMethod>().HasIndex(x => x.Name).IsUnique();
        // Vòng 2: mã voucher duy nhất; mỗi khách chỉ có tối đa 1 dòng cho 1 sản phẩm trong giỏ.
        mb.Entity<Voucher>().HasIndex(v => v.Code).IsUnique();
        mb.Entity<CartItem>().HasIndex(c => new { c.CustomerId, c.ProductId }).IsUnique();
    }
}
