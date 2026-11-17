using ComputerStore.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace ComputerStore.Data;

public static class DbSeeder
{
    public static void Seed(AppDbContext db)
    {
        // Vòng 2: dùng Migrations thay cho EnsureCreated (xem Data/Migrations).
        // Migrate() tạo bảng theo migration Initial + ghi bảng __EFMigrationsHistory.
        // LƯU Ý: DB cũ tạo bằng EnsureCreated (không có bảng lịch sử migration) sẽ
        // lỗi khi Migrate vì bảng đã tồn tại — xóa file DB dev đó và chạy lại
        // (dữ liệu dev chỉ là seed nên tạo lại được). Xem DEPLOY.md §5.
        db.Database.Migrate();

        // ---- Mật khẩu tài khoản demo (A-3) ----
        // Đọc từ biến môi trường; chỉ khi KHÔNG đặt biến môi trường mới dùng mật khẩu
        // demo mặc định dưới đây. Đây là mật khẩu DEMO-ONLY cho môi trường dev/kiểm thử:
        // production BẮT BUỘC đặt TECHPARTS_ADMIN_PASSWORD / TECHPARTS_CUSTOMER_PASSWORD
        // (môi trường khác nhau thì đặt trước lần chạy đầu tiên — seed chỉ chạy khi
        // nhóm dữ liệu đó còn trống, mật khẩu đã seed sẽ không bị ghi đè).
        var adminPassword = Environment.GetEnvironmentVariable("TECHPARTS_ADMIN_PASSWORD");
        if (string.IsNullOrWhiteSpace(adminPassword)) adminPassword = "Admin@123";
        var customerPassword = Environment.GetEnvironmentVariable("TECHPARTS_CUSTOMER_PASSWORD");
        if (string.IsNullOrWhiteSpace(customerPassword)) customerPassword = "Khach@123";

        var hasher = new PasswordHasher<AppUser>();

        // Seed bọc trong MỘT transaction và kiểm tra từng nhóm dữ liệu riêng (B-12):
        // nếu lần seed trước dở dang (đã lưu user nhưng chưa lưu sản phẩm), lần chạy sau
        // vẫn seed tiếp các nhóm còn thiếu thay vì thoát ngay vì Users.Any().
        using var tx = db.Database.BeginTransaction();
        try
        {
            // ---- Tài khoản ----
            if (!db.Users.Any())
            {
                var admin = new AppUser { Username = "admin", Role = Roles.Admin, DisplayName = "Quản trị viên" };
                admin.PasswordHash = hasher.HashPassword(admin, adminPassword);

                var cus = new Customer { FullName = "Nguyễn Văn A", Email = "khach@example.com", Phone = "0905123456", Address = "12 Lê Lợi, TP. Huế" };
                var user = new AppUser { Username = "khach", Role = Roles.Customer, DisplayName = cus.FullName, Customer = cus };
                user.PasswordHash = hasher.HashPassword(user, customerPassword);

                db.Users.AddRange(admin, user);
                db.SaveChanges();
            }

            // ---- Danh mục ----
            if (!db.Categories.Any())
            {
                db.Categories.AddRange(
                    new Category { Name = "CPU - Bộ vi xử lý" }, new Category { Name = "Mainboard - Bo mạch chủ" },
                    new Category { Name = "RAM - Bộ nhớ trong" }, new Category { Name = "VGA - Card đồ họa" },
                    new Category { Name = "SSD - Ổ cứng thể rắn" }, new Category { Name = "HDD - Ổ cứng cơ" },
                    new Category { Name = "PSU - Nguồn máy tính" }, new Category { Name = "Case - Vỏ máy" },
                    new Category { Name = "Tản nhiệt" }, new Category { Name = "Màn hình" },
                    new Category { Name = "Bàn phím" }, new Category { Name = "Chuột" });
                db.SaveChanges();
            }

            if (!db.ProductGroups.Any())
            {
                db.ProductGroups.AddRange(
                    new ProductGroup { Name = "Linh kiện chính", Description = "CPU, Mainboard, RAM, VGA" },
                    new ProductGroup { Name = "Lưu trữ", Description = "SSD, HDD" },
                    new ProductGroup { Name = "Nguồn - Vỏ - Tản nhiệt" },
                    new ProductGroup { Name = "Thiết bị ngoại vi", Description = "Màn hình, bàn phím, chuột" });
                db.SaveChanges();
            }

            if (!db.Brands.Any())
            {
                db.Brands.AddRange(
                    new Brand { Name = "Intel" }, new Brand { Name = "AMD" }, new Brand { Name = "ASUS" },
                    new Brand { Name = "MSI" }, new Brand { Name = "Gigabyte" }, new Brand { Name = "Corsair" },
                    new Brand { Name = "Kingston" }, new Brand { Name = "Samsung" }, new Brand { Name = "Logitech" });
                db.SaveChanges();
            }

            if (!db.PaymentMethods.Any())
            {
                db.PaymentMethods.AddRange(
                    new PaymentMethod { Name = "Thanh toán khi nhận hàng (COD)" },
                    new PaymentMethod { Name = "Chuyển khoản ngân hàng" },
                    new PaymentMethod { Name = "Thẻ tín dụng / ghi nợ" },
                    new PaymentMethod { Name = "Ví điện tử MoMo" });
                db.SaveChanges();
            }

            // ---- Sản phẩm ----
            if (!db.Products.Any())
            {
                int C(string n) => db.Categories.First(x => x.Name.StartsWith(n)).Id;
                int G(string n) => db.ProductGroups.First(x => x.Name == n).Id;
                int B(string n) => db.Brands.First(x => x.Name == n).Id;

                Product P(string name, string sku, decimal price, int stock, string cat, string grp, string brand, string spec, int warranty = 36) =>
                    new Product
                    {
                        Name = name, Sku = sku, Price = price, Stock = stock, WarrantyMonths = warranty,
                        CategoryId = C(cat), GroupId = G(grp), BrandId = B(brand), Specifications = spec,
                        Description = "Hàng chính hãng, bảo hành theo tiêu chuẩn nhà sản xuất."
                    };

                db.Products.AddRange(
                    P("Intel Core i5-13400F", "CPU-I5-13400F", 4690000, 25, "CPU", "Linh kiện chính", "Intel", "10 nhân 16 luồng, 2.5GHz up to 4.6GHz, socket LGA1700"),
                    P("Intel Core i7-14700K", "CPU-I7-14700K", 10990000, 12, "CPU", "Linh kiện chính", "Intel", "20 nhân 28 luồng, up to 5.6GHz, socket LGA1700"),
                    P("AMD Ryzen 5 7600X", "CPU-R5-7600X", 5490000, 18, "CPU", "Linh kiện chính", "AMD", "6 nhân 12 luồng, up to 5.3GHz, socket AM5"),
                    P("ASUS ROG STRIX B760-F GAMING WIFI", "MB-B760-F", 5990000, 10, "Mainboard", "Linh kiện chính", "ASUS", "Socket LGA1700, DDR5, WiFi 6E, ATX"),
                    P("MSI MAG B650 TOMAHAWK WIFI", "MB-B650-TW", 6290000, 8, "Mainboard", "Linh kiện chính", "MSI", "Socket AM5, DDR5, WiFi 6E, ATX"),
                    P("Kingston Fury Beast 16GB DDR5 5600MHz", "RAM-KF-16D5", 1390000, 40, "RAM", "Linh kiện chính", "Kingston", "1 thanh 16GB, DDR5, bus 5600MHz", 60),
                    P("Corsair Vengeance 32GB (2x16GB) DDR5 6000", "RAM-CV-32D5", 2990000, 20, "RAM", "Linh kiện chính", "Corsair", "Kit 2x16GB, DDR5, bus 6000MHz", 60),
                    P("Gigabyte GeForce RTX 4060 Gaming OC 8GB", "VGA-4060-GOC", 8590000, 9, "VGA", "Linh kiện chính", "Gigabyte", "8GB GDDR6, 3 fan, DLSS 3"),
                    P("Samsung 980 PRO 1TB NVMe", "SSD-980P-1TB", 2790000, 30, "SSD", "Lưu trữ", "Samsung", "PCIe 4.0 x4, đọc 7000MB/s, ghi 5000MB/s", 60),
                    P("Corsair RM750e 750W 80 Plus Gold", "PSU-RM750E", 2490000, 15, "PSU", "Nguồn - Vỏ - Tản nhiệt", "Corsair", "750W, Full modular, 80 Plus Gold", 84),
                    P("Logitech G102 Lightsync", "MOU-G102", 399000, 50, "Chuột", "Thiết bị ngoại vi", "Logitech", "8000 DPI, RGB, 6 nút", 24),
                    P("ASUS TUF Gaming VG249Q1A 24\" 165Hz", "MON-VG249", 3990000, 14, "Màn hình", "Thiết bị ngoại vi", "ASUS", "24 inch IPS, Full HD, 165Hz, 1ms", 36));

                db.SaveChanges();

                // Ảnh minh họa + giá gốc cho các món đang giảm giá
                var promos = new (string Sku, decimal OldPrice)[]
                {
                    ("MOU-G102", 449000m), ("SSD-980P-1TB", 2990000m), ("MON-VG249", 4390000m),
                    ("VGA-4060-GOC", 9490000m), ("RAM-KF-16D5", 1590000m), ("RAM-CV-32D5", 3290000m)
                };
                foreach (var (sku, old) in promos)
                    db.Products.First(p => p.Sku == sku).OldPrice = old;
                foreach (var p in db.Products)
                    p.ImageUrl = $"https://picsum.photos/seed/{p.Sku}/600/400";
                db.SaveChanges();
            }

            // ---- Tin tức mẫu (A-5): nội dung hướng dẫn chung, không phải tin thời sự ----
            if (!db.NewsPosts.Any())
            {
                db.NewsPosts.AddRange(
                    new NewsPost
                    {
                        Title = "Socket LGA1700 và AM5: nên chọn nền tảng nào?",
                        Summary = "Hai nền tảng phổ biến hiện nay khác nhau ở CPU, mainboard và khả năng nâng cấp. Bài viết giúp bạn đối chiếu trước khi chọn.",
                        Content = "CPU và mainboard phải cùng socket thì mới lắp được với nhau. Intel thế hệ 12/13/14 dùng socket LGA1700, còn AMD Ryzen 7000 dùng socket AM5; hai socket này không lắp chéo được.\n\nCả hai nền tảng đều dùng RAM DDR5, vì vậy khi chọn RAM cần xem mainboard hỗ trợ DDR4 hay DDR5 rồi mới mua. RAM DDR5 không cắm vừa khe DDR4 và ngược lại.\n\nKhi chọn nền tảng, hãy cân nhắc: ngân sách hiện tại, nhu cầu nâng cấp CPU sau này, và giá mainboard/RAM đi kèm. Nếu chưa chắc chắn, hãy dùng trang Build PC của TechParts để kiểm tra socket CPU và mainboard có khớp nhau không, hoặc gọi hotline 1900 0000 để được tư vấn.",
                        ImageUrl = "https://picsum.photos/seed/news-socket/600/300",
                        PublishedAt = new DateTime(2026, 8, 30), IsPublished = true
                    },
                    new NewsPost
                    {
                        Title = "DDR4 hay DDR5: khác nhau ở đâu, khi nào đáng nâng?",
                        Summary = "DDR5 có tốc độ cao hơn DDR4 nhưng giá cũng cao hơn và cần mainboard hỗ trợ. Không phải máy nào cũng cần nâng.",
                        Content = "DDR4 và DDR5 là hai chuẩn RAM khác nhau về chân cắm và điện áp, nên không thể thay thế trực tiếp cho nhau trên cùng một mainboard. Trước khi mua RAM, hãy đọc thông số mainboard để biết nó hỗ trợ DDR4 hay DDR5, bus tối đa bao nhiêu.\n\nVề hiệu năng, DDR5 có bus cao hơn (thường từ 4800MHz trở lên) và dung lượng mỗi thanh lớn hơn, phù hợp với máy mới dùng CPU đời mới. Với máy văn phòng hoặc máy cũ đang chạy DDR4 ổn định, việc nâng cấp lên DDR5 đồng nghĩa với thay cả mainboard và CPU nên chi phí cao mà hiệu quả chưa chắc đã thấy rõ.\n\nKinh nghiệm chung: nếu build máy mới thì ưu tiên DDR5; nếu máy đang dùng DDR4 mà chỉ thiếu dung lượng, hãy mua thêm RAM cùng chuẩn DDR4, cùng bus với thanh đang có.",
                        ImageUrl = "https://picsum.photos/seed/news-ddr/600/300",
                        PublishedAt = new DateTime(2026, 8, 26), IsPublished = true
                    },
                    new NewsPost
                    {
                        Title = "Kinh nghiệm chọn nguồn máy tính, tránh hàng công suất ảo",
                        Summary = "Nguồn là linh kiện ít được chú ý nhất nhưng lại ảnh hưởng trực tiếp tới độ ổn định và tuổi thọ của cả bộ máy.",
                        Content = "Công suất ghi trên vỏ hộp chưa chắc đã là công suất thật. Hãy ưu tiên nguồn có chứng nhận 80 Plus (Bronze, Gold...) từ thương hiệu quen thuộc và đọc kỹ công suất định mức (rated power), không chỉ công suất tối đa (peak power).\n\nCách chọn công suất: cộng công suất tiêu thụ của CPU và card đồ họa, cộng thêm khoảng 100-150W cho các linh kiện còn lại, rồi chọn nguồn có công suất định mức cao hơn tổng đó khoảng 20-30% để máy chạy mát và còn dư cho nâng cấp sau này. Ví dụ bộ máy dùng RTX 4060 thường dùng nguồn 650-750W là đủ.\n\nNgoài ra nên chọn nguồn có quạt êm, dây modular (tháo rời được) để đi dây gọn, và bảo hành dài (5 năm trở lên là dấu hiệu tốt). Nguồn kém chất lượng có thể gây sập máy, hỏng linh kiện, vì vậy đừng tiết kiệm quá mức ở linh kiện này.",
                        ImageUrl = "https://picsum.photos/seed/news-psu/600/300",
                        PublishedAt = new DateTime(2026, 8, 22), IsPublished = true
                    });
                db.SaveChanges();
            }

            // ---- Quy tắc phí ship + voucher mẫu (vòng 2) ----
            // Voucher mẫu DEMO để kiểm thử tính năng; admin có thể sửa/xóa ở /Admin/Vouchers.
            // Quy tắc ship mặc định: miễn phí từ 2.000.000₫ trở lên, dưới mức đó phí 30.000₫.
            if (!db.ShippingRules.Any())
            {
                db.ShippingRules.Add(new ShippingRule
                {
                    Name = "Giao hàng tiêu chuẩn", FreeFromAmount = 2000000, Fee = 30000,
                    IsActive = true, SortOrder = 1
                });
                db.SaveChanges();
            }

            if (!db.Vouchers.Any())
            {
                db.Vouchers.AddRange(
                    new Voucher
                    {
                        Code = "WELCOME10", DiscountType = DiscountType.Percent, DiscountValue = 10,
                        MinOrderAmount = 500000, MaxDiscountAmount = 500000,
                        ValidFrom = new DateTime(2026, 1, 1),
                        ValidTo = new DateTime(2026, 12, 31, 23, 59, 59),
                        MaxUses = 100, UsedCount = 0, IsActive = true
                    },
                    new Voucher
                    {
                        Code = "GIAM50K", DiscountType = DiscountType.Fixed, DiscountValue = 50000,
                        MinOrderAmount = 1000000, MaxDiscountAmount = null,
                        ValidFrom = new DateTime(2026, 1, 1),
                        ValidTo = new DateTime(2026, 12, 31, 23, 59, 59),
                        MaxUses = 50, UsedCount = 0, IsActive = true
                    });
                db.SaveChanges();
            }

            // ---- Đơn hàng mẫu cho khách ----
            if (!db.Orders.Any())
            {
                var cus = db.Customers.FirstOrDefault(c => c.Email == "khach@example.com")
                          ?? db.Customers.FirstOrDefault();
                if (cus != null)
                {
                    var p1 = db.Products.First(p => p.Sku == "CPU-I5-13400F");
                    var p2 = db.Products.First(p => p.Sku == "RAM-KF-16D5");
                    var order = new Order
                    {
                        Customer = cus, PaymentMethodId = db.PaymentMethods.First().Id,
                        ReceiverName = cus.FullName, Phone = cus.Phone, ShippingAddress = cus.Address,
                        Status = OrderStatus.Pending,
                        Details =
                        {
                            new OrderDetail { Product = p1, Quantity = 1, UnitPrice = p1.Price },
                            new OrderDetail { Product = p2, Quantity = 2, UnitPrice = p2.Price }
                        }
                    };
                    order.Total = order.Details.Sum(d => d.Quantity * d.UnitPrice);
                    p1.Stock -= 1; p2.Stock -= 2;
                    db.Orders.Add(order);
                    db.SaveChanges();
                }
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
