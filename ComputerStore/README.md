# TechParts – Hệ thống quản lý và bán linh kiện máy tính (ASP.NET Core MVC)

Web bán linh kiện máy tính gồm **trang khách hàng** và **trang quản trị (Admin)**.
Công nghệ: ASP.NET Core MVC (.NET 8) · Entity Framework Core · SQL Server · Bootstrap 5 · Cookie Authentication (phân quyền Admin / Customer).

## Nhóm thực hiện

- **Môn:** Ngôn ngữ lập trình C# — Trường Đại học Sài Gòn, Khoa Công nghệ thông tin, lớp DCT124C3
- **Đề tài:** Xây dựng hệ thống quản lý và bán linh kiện máy tính
- **Nhóm 13:**
  - 3124411034 – Phạm Hoàng Thiên Bảo
  - 3124411168 – Trần Quang Minh
  - 3124411183 – Đinh Lê Hoàng Nghĩa
- **GV hướng dẫn:** Thầy Đỗ Như Tài

## 1. Kiến trúc (ghi trung thực)

Dự án là **một ứng dụng web ASP.NET Core MVC duy nhất** (`ComputerStore.csproj`).
Phần quản trị được làm bằng **Admin Area trong cùng ứng dụng web** (`Areas/Admin/`), **không có** project WinForms riêng và **không có** project/API riêng như thiết kế ban đầu ở Lần 1 — đây là điểm lệch so với thiết kế Lần 1, đã ghi nhận trong `FIX_LOG.md` (xem mục "Điểm lệch thiết kế Lần 1 so với code thực tế").

Luồng dữ liệu: trình duyệt → Controller (MVC) → EF Core → SQL Server (LocalDB khi chạy dev trên Windows).
Giỏ hàng: khách đã đăng nhập lưu vào database (bảng `CartItems`, theo tài khoản, không mất khi hết session/đăng xuất); khách vãng lai dùng Session (in-memory, hết hạn sau 2 giờ); khi đăng nhập, giỏ session được gộp vào giỏ database (cộng số lượng, kẹp theo tồn kho). Có phí ship + mã giảm giá (tính lại phía server lúc checkout); thanh toán COD và chuyển khoản (thủ công). **Thanh toán online không thực hiện theo yêu cầu của nhóm — chỉ dùng COD/chuyển khoản** (xem §5/§6/§10).

## 2. Yêu cầu
- .NET 8 SDK
- SQL Server (LocalDB đi kèm Visual Studio, hoặc SQL Server Express)
- Visual Studio 2022 / VS Code

## 3. Chạy dự án
1. Mở `appsettings.json`, sửa `ConnectionStrings:DefaultConnection` cho đúng máy của bạn.
   - LocalDB (mặc định): `Server=(localdb)\\MSSQLLocalDB;Database=ComputerStoreDb;Trusted_Connection=True;TrustServerCertificate=True`
   - SQL Express: `Server=.\\SQLEXPRESS;Database=ComputerStoreDb;Trusted_Connection=True;TrustServerCertificate=True`
2. Chạy:
   ```
   dotnet restore
   dotnet run
   ```
   Hoặc mở file `.csproj` trong Visual Studio và nhấn F5.
3. Lần chạy đầu tiên ứng dụng **tự tạo database bằng Migrations (`db.Database.Migrate()`) và dữ liệu mẫu** (12 loại, 4 nhóm, 9 thương hiệu, 12 linh kiện, 3 bài tin tức, 1 đơn mẫu...). Xem §9.

## 4. Tài khoản demo

| Vai trò | Tên đăng nhập |
|---|---|
| Admin | `admin` |
| Khách hàng | `khach` |

**Mật khẩu demo không ghi trong README này.** Mật khẩu mặc định khi chạy lần đầu (chỉ dùng cho môi trường dev/kiểm thử) là mật khẩu demo đi kèm mã nguồn trong `Data/DbSeeder.cs` — xem comment trong file đó. Để đổi mật khẩu seed, đặt biến môi trường **trước lần chạy đầu tiên** (lần tạo database):

| Biến môi trường | Tài khoản |
|---|---|
| `TECHPARTS_ADMIN_PASSWORD` | `admin` |
| `TECHPARTS_CUSTOMER_PASSWORD` | `khach` |

Ví dụ (PowerShell):
```powershell
$env:TECHPARTS_ADMIN_PASSWORD = "MatKhauManh@2026"
$env:TECHPARTS_CUSTOMER_PASSWORD = "MatKhauKhach@2026"
dotnet run
```
**Production bắt buộc đặt hai biến môi trường trên** (xem `DEPLOY.md`). Lưu ý: seed chỉ tạo tài khoản khi database còn trống; mật khẩu đã tạo rồi sẽ không bị ghi đè khi chạy lại. Admin đăng nhập xong sẽ vào `/Admin`. Khách hàng có thể tự đăng ký tại `/Account/Register`.

## 5. Chức năng (chỉ liệt kê những gì code thật sự làm được)

### Trang khách hàng
- Trang chủ: danh mục, banner, mục Đang giảm giá, từng nhóm sản phẩm (lọc theo hãng/khoảng giá), khối Build PC, tin tức (bài thật từ database), khối liên hệ.
- Danh sách sản phẩm: tìm kiếm theo tên/SKU, lọc theo loại/nhóm/hãng/khoảng giá, lọc chỉ món giảm giá (`?discounted=true`), sắp xếp, phân trang 12/trang.
- Chi tiết sản phẩm + sản phẩm cùng loại.
- **Build PC** (`/BuildPc`): chọn CPU, Mainboard, RAM, VGA, PSU, ổ cứng, Case; hiển thị tổng tiền; kiểm tra tương thích socket CPU/mainboard và chuẩn RAM (DDR4/DDR5) bằng cách trích token từ thông số kỹ thuật; nút "Thêm cả bộ vào giỏ".
- **Tin tức** (`/News`): danh sách + trang chi tiết bài viết (quản lý ở `/Admin/News`).
- **Để lại số, chờ gọi lại** (`/Callback/Create`): form tên + SĐT + nội dung cần tư vấn, lưu vào database; admin xem và đánh dấu đã xử lý ở `/Admin/CallbackRequests`.
- Giỏ hàng bền: khách đã đăng nhập lưu vào database, không mất khi đăng xuất/hết session; khách vãng lai dùng session; đăng nhập thì gộp giỏ session vào giỏ database (cộng số lượng, kẹp theo tồn kho, báo rõ món nào bị kẹp). Số lượng trên biểu tượng giỏ ở header hiển thị đúng cho cả hai loại khách.
- Đặt hàng + chọn hình thức thanh toán, lịch sử đơn hàng, hủy đơn (chỉ đơn "Chờ xác nhận"), sửa thông tin cá nhân.
- **Đổi mật khẩu** khi đã đăng nhập (nhập mật khẩu cũ + mật khẩu mới; tối thiểu 8 ký tự, ít nhất 1 chữ + 1 số) và **quên mật khẩu** bằng link reset gửi qua email: token ngẫu nhiên, chỉ lưu hash SHA256, hết hạn 1 giờ, dùng 1 lần. Cấu hình SMTP ở section `Smtp` trong `appsettings.json`; chưa cấu hình SMTP thì ở môi trường Development link reset chỉ ghi vào log (không hiện trên màn hình, không gửi mail thật).
- **Phí ship + mã giảm giá** (`/Cart/Checkout`): phí ship mặc định miễn phí từ 2.000.000₫ trở lên, dưới mức đó 30.000₫ (quy tắc sửa được ở `/Admin/ShippingRules`); nhập mã giảm giá rồi bấm "Áp dụng" — giảm % (có trần) hoặc giảm số tiền cố định, chỉ tính trên tạm tính; phí ship/giảm giá/tổng cuối được tính lại phía server lúc đặt hàng, không tin số client gửi. Mã mẫu (demo): `WELCOME10` (giảm 10%, tối đa 500.000₫, đơn từ 500.000₫, 100 lượt, hạn tới 31/12/2026), `GIAM50K` (giảm 50.000₫, đơn từ 1.000.000₫, 50 lượt, hạn tới 31/12/2026).
- **Thanh toán:** checkout chỉ liệt kê **COD** và **chuyển khoản ngân hàng** (thủ công). Chuyển khoản: trang thành công hiện hướng dẫn liên hệ hotline 1900 0000 để nhận thông tin chuyển khoản (không có số tài khoản nào trong code — nhóm không làm thanh toán online nên không có tài khoản nhận chuyển khoản cấu hình sẵn). **Thẻ tín dụng / ví MoMo chỉ là tên trong database, không hiện ở checkout. Thanh toán online không thực hiện theo yêu cầu của nhóm (quyết định ngày 2026-11-15: bỏ hẳn mục thanh toán online, không có cổng thanh toán, không có VNPay) — chỉ dùng COD/chuyển khoản.**
- Đăng ký / đăng nhập / đăng xuất. Đăng nhập sai 5 lần liên tiếp thì tài khoản bị khóa 15 phút.

### Trang quản trị (`/Admin`)
| Chức năng | Đường dẫn |
|---|---|
| Tổng quan: doanh thu (đơn hoàn thành), đơn chờ, hàng sắp hết | `/Admin/Dashboard` |
| Thêm/sửa/xóa **linh kiện** (có upload ảnh, kiểm tra magic bytes) | `/Admin/Products` |
| Thêm/sửa/xóa **loại / nhóm / thương hiệu / hình thức thanh toán** | `/Admin/Categories`, `/Admin/Groups`, `/Admin/Brands`, `/Admin/PaymentMethods` |
| Thêm/sửa **thông tin khách hàng** | `/Admin/Customers` |
| Quản lý **quy tắc phí vận chuyển** | `/Admin/ShippingRules` |
| Quản lý **mã giảm giá** | `/Admin/Vouchers` |
| **Nhập kho** (phiếu nhập nhiều dòng, tự cộng tồn kho; **sửa/xóa phiếu** đảo ngược đúng tồn kho, chặn khi làm tồn kho âm) | `/Admin/StockReceipts` |
| Quản lý đơn hàng, đổi trạng thái, xác nhận thanh toán | `/Admin/Orders` |
| Yêu cầu gọi lại (đánh dấu đã xử lý) | `/Admin/CallbackRequests` |
| Quản lý tin tức | `/Admin/News` |

## 6. Quy tắc nghiệp vụ đã cài sẵn
- **Checkout chống bán vượt kho:** toàn bộ checkout chạy trong một transaction tường minh; tồn kho trừ bằng lệnh `UPDATE ... WHERE Stock >= Quantity` ở tầng database và kiểm tra số dòng bị ảnh hưởng — nếu có khách khác vừa mua hết hàng giữa chừng thì đơn không được tạo và người dùng được báo rõ. Xem `Controllers/CartController.cs`.
- **Máy trạng thái đơn hàng:** Pending → Confirmed → Shipping → Completed; từ Pending/Confirmed/Shipping → Cancelled. Completed và Cancelled là trạng thái cuối. Mọi chuyển đổi khác đều bị từ chối. Không đổi được trạng thái thanh toán trên đơn đã hủy. Xem `Helpers/Helpers.cs` (`OrderTransitions`) và `Areas/Admin/Controllers/OrdersController.cs`.
- **Thời điểm trừ kho (thực tế của code):** tồn kho được trừ lúc **checkout** (đặt hàng), không phải lúc admin duyệt đơn. Admin duyệt đơn chỉ đổi trạng thái (Completed thì `IsPaid = true`); hủy đơn thì hoàn kho. Lý do: đơn chỉ được tạo khi còn hàng, tránh đơn Pending nhưng kho đã hết (xem `FIX_LOG.md`).
- Không xóa Loại/Nhóm/Thương hiệu đang có linh kiện; không xóa Hình thức thanh toán đã có đơn hàng.
- Linh kiện đã phát sinh đơn hàng/phiếu nhập thì không xóa được, chỉ **ẩn** (bỏ tick "Đang bán").
- Tồn kho **chỉ thay đổi** qua Nhập kho (cộng), Đặt hàng (trừ), Hủy đơn (hoàn lại).
- Khách chỉ hủy được đơn "Chờ xác nhận".
- Khách hàng: chỉ Thêm/Sửa (không xóa để bảo toàn lịch sử đơn hàng).
- Phiếu nhập: một sản phẩm chỉ xuất hiện tối đa 1 dòng trong cùng một phiếu; trùng dòng bị từ chối kèm thông báo.
- Đăng ký kiểm tra trùng tên đăng nhập, Email và SĐT.
- Giá bán 0đ được hiểu là linh kiện tặng kèm (không cấm, ghi rõ để tránh nhầm là lỗi nhập liệu).
- Mật khẩu được băm bằng `PasswordHasher`; các form POST đều có chống CSRF; upload ảnh kiểm tra cả đuôi file và magic bytes (JPEG/PNG/GIF/WebP), tối đa 5MB.
- Giỏ hàng tự kẹp/bỏ dòng theo tồn kho hiện tại và **báo cho người dùng biết** món nào đã thay đổi (thông báo vàng trên trang giỏ).
- **Phí ship:** lấy quy tắc đang áp dụng có thứ tự nhỏ nhất; tạm tính ≥ ngưỡng "Miễn phí từ" thì miễn phí, ngược lại tính phí; không có quy tắc nào đang áp dụng thì phí = 0.
- **Voucher:** kiểm tra tồn tại → đang áp dụng → trong hạn → còn lượt → đủ đơn tối thiểu; số lượt đã dùng (`UsedCount`) chỉ tăng khi đơn được tạo thành công, trong cùng transaction tạo đơn. Giảm % kẹp bởi giảm tối đa (nếu có); giảm cố định không vượt tạm tính; tổng cuối = tạm tính + phí ship − giảm giá, không âm.
- **Thanh toán:** chỉ COD và chuyển khoản mới hiện ở checkout (thẻ/MoMo chỉ là tên trong database, không hiện). Đơn lưu `ShippingFee`, `DiscountAmount`, `VoucherCode`; `Total` là tổng cuối khách phải trả. Nhóm không làm thanh toán online (quyết định 2026-11-15).

## 7. Cấu trúc thư mục
```
Areas/Admin/        Controllers + Views của trang quản trị
Controllers/        Home, Shop, Cart, Account, BuildPc, News, Callback (phía khách hàng)
Data/               AppDbContext, DbSeeder (dữ liệu mẫu), Migrations/ (migration Initial),
                    DesignTimeDbContextFactory.cs (phục vụ dotnet-ef)
Models/             Entities.cs, ViewModels.cs, ShippingRule.cs, Voucher.cs,
                    CartItem.cs (giỏ hàng bền), PasswordResetToken.cs (token quên mật khẩu),
                    PasswordViewModels.cs, StockReceiptEditVm.cs
Services/           CartService.cs (giỏ database + gộp giỏ session, logic thuần CartMerge),
                    EmailService.cs (IEmailService: SMTP thật / ghi log ở Development),
                    OrderPricingService.cs (tính phí ship/giảm giá, logic thuần),
                    VoucherService.cs (validate mã)
                    (Không có thư mục Payment/ — nhóm không làm thanh toán online.)
Helpers/            Session giỏ hàng, định dạng tiền/trạng thái, hủy đơn,
                    máy trạng thái đơn hàng (OrderTransitions), kiểm tra tương thích Build PC,
                    StockReceiptStock.cs (kiểm tra đảo ngược tồn kho khi sửa/xóa phiếu nhập),
                    ProductImageHelper.cs (ảnh placeholder local theo nhóm linh kiện)
Views/              Giao diện khách hàng
wwwroot/            CSS, ảnh (kể cả 7 ảnh placeholder SVG theo nhóm linh kiện), thư mục uploads
```

## 8. Sơ đồ dữ liệu
```
Category, ProductGroup, Brand  1───n  Product
Product  1───n  OrderDetail  n───1  Order  n───1  Customer
Order    n───1  PaymentMethod
Product  1───n  StockReceiptDetail  n───1  StockReceipt
AppUser  n───0..1  Customer          (Admin không có Customer)
NewsPost, CallbackRequest           (bảng độc lập, không khóa ngoại)
ShippingRule, Voucher               (bảng độc lập, không khóa ngoại; Order chỉ sao chép
                                     ShippingFee/DiscountAmount/VoucherCode vào đơn)
```
Mô hình thật có 17 bảng (13 bảng cũ + `ShippingRules` + `Vouchers` + `CartItems` + `PasswordResetTokens`).
`CartItem` có unique index (CustomerId, ProductId); `Voucher.Code` có unique index.

## 9. Migrations (EF Core) — đã áp dụng mặc định
Ứng dụng dùng **Migrations thay cho `EnsureCreated()`**: lúc khởi động, `DbSeeder.Seed` gọi `db.Database.Migrate()` rồi mới seed dữ liệu mẫu. Migration `Initial` (`Data/Migrations/20261115100151_Initial.cs`, 17 bảng) được scaffold bằng SQL Server; xem `FIX_LOG_2.md` §5 để biết 2 chỗ sửa tay để migration chạy được cả trên SQLite (kiểm chứng trong môi trường test).
- Trên SQL Server: tạo database rỗng `ComputerStoreDb` rồi chạy ứng dụng lần đầu (tự Migrate + seed), hoặc chạy `dotnet ef database update` (xem `DEPLOY.md`).
- Thêm migration mới sau khi đổi model: `dotnet ef migrations add <TenMigration>` rồi `dotnet ef database update`.
- **Database cũ tạo bằng `EnsureCreated()` không tương thích với Migrate** (bảng đã tồn tại nhưng không có `__EFMigrationsHistory`): xóa database cũ và chạy lại, hoặc tạo database mới. Xem `DEPLOY.md` §3.

## 10. Hạn chế đã biết
- **Build PC chỉ kiểm tra được socket CPU/mainboard và chuẩn RAM** khi thông số kỹ thuật ghi rõ dạng "socket LGA1700", "socket AM5", "DDR5"... Nếu không trích được token, hệ thống báo "không xác định được, vui lòng kiểm tra lại" thay vì đoán. Chưa kiểm tra công suất nguồn so với VGA hay kích thước case (thông số mẫu không ghi đủ dữ liệu này).
- Ảnh sản phẩm mẫu dùng `picsum.photos` (ảnh ngẫu nhiên ngoài internet, không phải ảnh thật). Khi ảnh không tải được (mất mạng / link hỏng), trang tự rơi về ảnh placeholder local theo nhóm linh kiện (`wwwroot/img/placeholder-cpu.svg`, `-ram`, `-vga`, `-mainboard`, `-storage`, `-psu`, `-monitor`, `no-image.svg`) nên shop không bao giờ hiện trang trắng/hỏng. **Cần ảnh thật:** đăng nhập admin → `/Admin/Products` → sửa sản phẩm → upload ảnh (JPEG/PNG/GIF/WebP, tối đa 5MB, kiểm tra magic bytes); ảnh lưu vào `wwwroot/uploads/`. Ảnh nên có kích thước khoảng 600×400 để hiển thị đẹp.
- Không làm thanh toán online (theo yêu cầu của nhóm, quyết định 2026-11-15): chỉ dùng COD và chuyển khoản thủ công; **thẻ tín dụng / ví MoMo chỉ là tên trong database, không hiện ở checkout**; chuyển khoản chưa có số tài khoản thật trong cấu hình (trang thành công chỉ hiện hướng dẫn liên hệ hotline 1900 0000).
- (Đã bỏ) Ghi chú cũ về `vnp_TxnRef`/VNPay không còn áp dụng — nhóm không làm thanh toán online.
- Không có VAT tách riêng — tổng đơn = tạm tính + phí ship − giảm giá.
- Không có đánh giá sản phẩm, báo cáo doanh thu theo tháng/biểu đồ, xuất Excel/PDF.
- Giỏ hàng của khách vãng lai vẫn chỉ sống trong session (mất khi hết hạn 2 giờ/restart server); chỉ khách đã đăng nhập mới có giỏ bền theo tài khoản.
- Đã kiểm chứng trên SQL Server 2022 thật (RTM-CU28, ngày 2026-11-15, trên máy Linux này): migration + seed đúng, sắp xếp theo giá và Dashboard doanh thu đều đúng (xem `TEST_LOG_2.md` §5). Nhóm vẫn nên chạy lại `SQLSERVER_CHECKLIST.md` trên Windows để xác nhận trên bản cài chuẩn LocalDB/SQL Server.
- Dữ liệu mẫu mỏng: 12 sản phẩm, thiếu hẳn HDD, Case, Tản nhiệt, Bàn phím; 1 khách hàng, 1 đơn mẫu.
- Chưa có lịch sử thay đổi trạng thái đơn (không ghi ai đổi trạng thái lúc nào).

## 11. Ghi chú giao diện
Bootstrap 5.3.3, Bootstrap Icons 1.11.3, jQuery 3.7.1, jQuery Validation 1.19.5 và jQuery Validation Unobtrusive 4.0.0 đã được đưa về file local trong `wwwroot/lib/`; hai layout `_Layout.cshtml` và `_AdminLayout.cshtml` đã trỏ sang file local. Nhờ đó giao diện hiển thị đầy đủ cả khi máy không có mạng.
