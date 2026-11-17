using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ComputerStore.Data;

/// <summary>
/// Factory để `dotnet ef` tạo/áp dụng migration mà KHÔNG chạy Program.cs
/// (tránh chạy DbSeeder khi scaffold). Cấu hình SQL Server — môi trường
/// production/chính thức của dự án (LocalDB trên máy dev Windows).
/// Tạo migration: dotnet ef migrations add &lt;Ten&gt;
/// Áp dụng trên SQL Server: dotnet ef database update
/// </summary>
public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\MSSQLLocalDB;Database=ComputerStoreDb;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }
}
